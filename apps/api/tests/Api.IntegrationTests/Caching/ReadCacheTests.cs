using System.Diagnostics.Metrics;
using Api.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Tenancy.Caching;

namespace Api.IntegrationTests.Caching;

// Section 3, "Cache": ReadCache keeps an answer in Valkey for its lifetime, and a removal by tag in one process is seen at once by
// every other, through Valkey alone. Without a cache connection, every read goes to the database; a cache that fails never fails a read.
public sealed class ReadCacheTests(Valkey valkey) : IClassFixture<Valkey>
{
    private static readonly UserCachedRead<Answer> UserRead =
        new("test-user-read", TimeSpan.FromMinutes(1), subject => [CacheTag.Of("test-user", subject)]);

    private static readonly TenantCachedRead<Answer> TenantRead =
        new("test-tenant-read", TimeSpan.FromMinutes(1), (tenantId, _) => [CacheTag.Of("test-tenant", tenantId)]);

    // A timer can fire up to one coarse clock tick early on the test's stopwatch: at most 10 ms on Linux (section 9, rule 13).
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(11);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Read_TheSameReadTwice_LoadsFromTheDatabaseOnceAndThenFromTheCache()
    {
        await using var host = CacheHosts.Start(valkey.ConnectionString);
        var database = new CountingDatabase();
        var subject = Subject();

        var first = await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);
        var second = await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);

        first.ShouldBe(new Answer(1));
        second.ShouldBe(new Answer(1));
        database.Loads.ShouldBe(1);
    }

    // Two processes share Valkey and nothing else. A tag's removal time is read from Valkey on each read, so the other process's next
    // read misses at once, with no wait for the expiry and no message between the processes.
    [Fact]
    public async Task RemoveTags_InOneProcess_TheOtherProcessReadsTheDatabaseAgainAtOnce()
    {
        await using var web = CacheHosts.Start(valkey.ConnectionString);
        await using var worker = CacheHosts.Start(valkey.ConnectionString);
        var database = new CountingDatabase();
        var subject = Subject();
        await web.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);
        await worker.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);

        await worker.Cache().RemoveTagsAsync([CacheTag.Of("test-user", subject)]);
        var afterRemoval = await web.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);

        afterRemoval.ShouldBe(new Answer(2));
        database.Loads.ShouldBe(2);
    }

    // Row level security does not protect the cache, so the key holds the tenant: the same subject in two tenants has two entries, and a
    // removal in one tenant leaves the other's entry as it is.
    [Fact]
    public async Task Read_OneSubjectInTwoTenants_KeepsTwoEntriesAndARemovalInOneLeavesTheOther()
    {
        await using var host = CacheHosts.Start(valkey.ConnectionString);
        var first = new CountingDatabase();
        var second = new CountingDatabase();
        var (firstTenant, secondTenant, subject) = (Guid.NewGuid(), Guid.NewGuid(), Subject());
        await host.Cache().ReadAsync(TenantRead, firstTenant, subject, first.LoadAsync, Cancellation);
        await host.Cache().ReadAsync(TenantRead, secondTenant, subject, second.LoadAsync, Cancellation);

        await host.Cache().RemoveTagsAsync([CacheTag.Of("test-tenant", firstTenant)]);
        await host.Cache().ReadAsync(TenantRead, firstTenant, subject, first.LoadAsync, Cancellation);
        await host.Cache().ReadAsync(TenantRead, secondTenant, subject, second.LoadAsync, Cancellation);

        first.Loads.ShouldBe(2);
        second.Loads.ShouldBe(1);
    }

    [Fact]
    public async Task Read_WithoutACacheConnection_LoadsFromTheDatabaseEveryTime()
    {
        await using var host = CacheHosts.Start(cacheConnection: null);
        var database = new CountingDatabase();
        var subject = Subject();

        await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);
        await host.Cache().RemoveTagsAsync([CacheTag.Of("test-user", subject)]);
        var second = await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);

        second.ShouldBe(new Answer(2));
        database.Loads.ShouldBe(2);
    }

    // A cache that is down needs no person, so its failure is a warning, never an error (section 7, "Error tracking", rule 4). The read
    // answers from the database. The failure opens the breaker, whose warning names it. A refused call also logs CacheReadFailed; a call that
    // times out is a miss for FusionCache and logs no more.
    [Fact]
    public async Task Read_ValkeyStopped_AnswersFromTheDatabaseAndLogsAWarning()
    {
        await using var stopping = new Valkey();
        await stopping.InitializeAsync();
        await using var host = CacheHosts.Start(stopping.ConnectionString);
        var database = new CountingDatabase();
        var subject = Subject();
        await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);

        await stopping.StopAsync();
        var read = await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);
        await host.Cache().RemoveTagsAsync([CacheTag.Of("test-user", subject)]);

        read.ShouldBe(new Answer(2));
        var logs = host.GetRequiredService<FakeLogCollector>().GetSnapshot();
        logs.Where(log => log.Level >= LogLevel.Error).ShouldBeEmpty();
        logs.Where(log => log.Level == LogLevel.Warning).Select(log => log.Id.Name).ShouldContain("CacheBreakerOpened");
        logs.Where(log => log.Level == LogLevel.Warning).Select(log => log.Id.Name).ShouldBeSubsetOf(["CacheBreakerOpened", "CacheReadFailed"]);
    }

    // A Valkey that answers nothing costs a read the time limit once. The failure opens FusionCache's circuit breaker, so for its duration
    // the next reads go straight to the database: none of them waits for Valkey, so none of them calls it (C10).
    [Fact]
    public async Task Read_ValkeySlow_WaitsOnceAndThenReadsTheDatabaseWithoutCallingValkey()
    {
        await using var slow = new Valkey();
        await slow.InitializeAsync();
        await using var host = CacheHosts.Start(slow.ConnectionString);
        var database = new CountingDatabase();
        var subject = Subject();
        await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);

        await slow.PauseAsync();
        var first = await TimedReadsAsync(host, subject, database, 1);
        var next = await TimedReadsAsync(host, subject, database, 5);
        await slow.UnpauseAsync();

        first.ShouldAllBe(elapsed => elapsed >= CachingExtensions.CallTimeLimit - Tolerance);
        next.ShouldAllBe(elapsed => elapsed < CachingExtensions.CallTimeLimit - Tolerance);
        database.Loads.ShouldBe(7);
    }

    // A tag's removal marker outlives the entries it outdates only while Valkey evicts entries first (C9). Step 42 shows Valkey's evicted
    // keys, from the host's meter, so an eviction under memory pressure is seen.
    [Fact]
    public async Task ReadEvictions_AFullValkey_ReportsItsEvictedKeys()
    {
        await using var small = Valkey.WithArguments("--maxmemory", "2mb", "--maxmemory-policy", "volatile-ttl");
        await small.InitializeAsync();
        await using var host = CacheHosts.Start(small.ConnectionString);
        using var evicted = new MetricCollector<long>(host.GetRequiredService<IMeterFactory>(), "Tenancy.Caching", "cache.evicted_keys");

        await host.Cache().ReadAsync(UserRead, Subject(), new CountingDatabase().LoadAsync, Cancellation);
        await FillAsync(small.ConnectionString, 4_000);
        evicted.RecordObservableInstruments();

        evicted.LastMeasurement!.Value.ShouldBeGreaterThan(0);
    }

    // The database is ours: a read that fails there fails as it would without a cache, and the cache is no cause.
    [Fact]
    public async Task Read_TheDatabaseFails_FailsWithTheDatabasesErrorAndLogsNothing()
    {
        await using var host = CacheHosts.Start(valkey.ConnectionString);

        var read = () => host.Cache().ReadAsync<Answer>(UserRead, Subject(), _ => throw new TimeoutException("The database did not answer."), Cancellation);

        (await read.ShouldThrowAsync<TimeoutException>()).Message.ShouldBe("The database did not answer.");
        host.GetRequiredService<FakeLogCollector>().GetSnapshot().Where(log => log.Level >= LogLevel.Warning).ShouldBeEmpty();
    }

    // Step 42 shows the hits and the misses of each kind of read, from the host's meter.
    [Fact]
    public async Task Read_AMissAndAHit_CountsEachByTheKindOfReadAndItsResult()
    {
        await using var host = CacheHosts.Start(valkey.ConnectionString);
        using var reads = new MetricCollector<long>(host.GetRequiredService<IMeterFactory>(), "Tenancy.Caching", "cache.reads");
        var database = new CountingDatabase();
        var subject = Subject();

        await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);
        await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);

        reads.GetMeasurementSnapshot()
            .Select(measurement => $"{measurement.Tags["cache.read"]} {measurement.Tags["cache.result"]} {measurement.Value}")
            .ShouldBe(["test-user-read miss 1", "test-user-read hit 1"]);
    }

    // As many keys of 1 KB with a time to live, written past Valkey's memory limit.
    private static async Task FillAsync(string connection, int count)
    {
        await using var valkey = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(connection);
        var value = new string('x', 1024);
        foreach (var index in Enumerable.Range(0, count))
        {
            await valkey.GetDatabase().StringSetAsync($"fill:{index}", value, TimeSpan.FromMinutes(10));
        }
    }

    // Each read on its own stopwatch, one after the other.
    private static async Task<List<TimeSpan>> TimedReadsAsync(IServiceProvider host, string subject, CountingDatabase database, int count)
    {
        var times = new List<TimeSpan>();
        foreach (var _ in Enumerable.Range(0, count))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await host.Cache().ReadAsync(UserRead, subject, database.LoadAsync, Cancellation);
            times.Add(watch.Elapsed);
        }

        return times;
    }

    private static string Subject() => $"user_{Guid.NewGuid():N}";

    public sealed record Answer(int Load);

    // Stands for the read of the database: each load answers its own number.
    private sealed class CountingDatabase
    {
        private int _loads;

        public int Loads => _loads;

        public Task<Answer> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(new Answer(Interlocked.Increment(ref _loads)));
    }
}
