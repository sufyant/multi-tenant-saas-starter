using Microsoft.Extensions.DependencyInjection;
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
    // answers from the database.
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
        logs.Where(log => log.Level == LogLevel.Warning).Select(log => log.Id.Name).ShouldBe(["CacheReadFailed", "CacheRemovalFailed"]);
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
