using System.Diagnostics;
using System.Net;
using Api.Caching;
using Microsoft.Extensions.DependencyInjection;
using Tenancy;
using Tenancy.Caching;
using Wolverine;

namespace Api.IntegrationTests.Caching;

// C6: the removal of a commit's cache tags is durable. The save writes a message in its own transaction, and Wolverine handles it
// CacheRemoval.Delay after the commit, at least once, until Valkey takes it. So the next requests see the new answer even when the removal
// at the commit never reached Valkey: a process whose circuit breaker is open, a crash right after the commit, or a read that began before
// the commit and wrote its old answer after the removal. Each test waits for the new answer far less than an entry lives (10 minutes).
// The message is scheduled, so each test runs on a database of its own: on the shared one, a host of another test runs it in its own cache
// (section 9, rule 10).
public sealed class CacheRemovalTests(Database database) : IAsyncLifetime
{
    private readonly Valkey _valkey = new();

    private string _databaseName = null!;

    private TestData _testData = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = await database.CreateMigratedDatabaseAsync();
        _testData = new TestData(database, _databaseName);
        await _valkey.InitializeAsync();
    }

    public ValueTask DisposeAsync() => _valkey.DisposeAsync();

    // A failure opens the writer's breaker, so its removal at the commit skips Valkey. The message reaches it anyway.
    [Fact]
    public async Task RemoveCacheTags_WhileTheWritersBreakerIsOpen_ReachesTheOtherProcess()
    {
        await using var writer = Host();
        await using var reader = Host();
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var admin = writer.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var before = await MembersAsync(reader, member, tenant.Id);
        await MembersAsync(writer, member, tenant.Id);

        await _valkey.PauseAsync();
        await MembersAsync(writer, member, tenant.Id);
        await _valkey.UnpauseAsync();
        (await admin.SuspendTenantAsync(tenant.Id)).EnsureSuccessStatusCode();
        var watch = Stopwatch.StartNew();
        await Waiting.UntilAsync(async () => await MembersAsync(reader, member, tenant.Id) == HttpStatusCode.Forbidden);

        before.ShouldBe(HttpStatusCode.OK);
        watch.Elapsed.ShouldBeLessThan(CachingExtensions.BreakerDuration);
    }

    // A crash between the commit and the removal at the commit leaves the old entry; the message, written before the commit, removes it.
    [Fact]
    public async Task RemoveCacheTags_WithoutTheRemovalAtTheCommit_ReachesTheOtherProcess()
    {
        await using var writer = Host(removeAtCommit: false);
        await using var reader = Host();
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var admin = writer.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var before = await MembersAsync(reader, member, tenant.Id);

        (await admin.SuspendTenantAsync(tenant.Id)).EnsureSuccessStatusCode();
        var rightAfter = await MembersAsync(reader, member, tenant.Id);
        await Waiting.UntilAsync(async () => await MembersAsync(reader, member, tenant.Id) == HttpStatusCode.Forbidden);

        (before, rightAfter).ShouldBe((HttpStatusCode.OK, HttpStatusCode.OK));
    }

    // A read that read the row before the commit writes its old answer after the commit. FusionCache stamps an entry with the moment its
    // read began, so the removal at the commit already outdates it; when that removal is lost, or the reader's clock runs ahead of the
    // remover's, the message comes CacheRemoval.Delay later and removes it (a delayed double delete). The removal at the commit is off here,
    // so only the message can remove it.
    [Fact]
    public async Task RemoveCacheTags_AReadThatBeganBeforeTheCommit_IsRemovedByTheMessage()
    {
        await using var api = Host(removeAtCommit: false);
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id);
        var userId = await _testData.UserIdOfAsync(member);
        var cache = api.Services.GetRequiredService<ReadCache>();
        var read = new UserCachedRead<Seen>("test-user-state", CachedRead.LongestLifetime, user => [CacheTag.Of(CacheLabel.User, user)]);
        var started = new TaskCompletionSource();
        var released = new TaskCompletionSource();
        var reading = cache.ReadAsync(read, member, async _ =>
        {
            started.SetResult();
            await released.Task;
            return new Seen("active");
        }, Cancellation);
        await started.Task;

        (await api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true).PostAsync($"/v1/platform/users/{userId}/suspend", null, Cancellation)).EnsureSuccessStatusCode();
        released.SetResult();
        await reading;
        var rightAfter = await cache.ReadAsync(read, member, _ => Task.FromResult(new Seen("suspended")), Cancellation);
        await Waiting.UntilAsync(async () => await cache.ReadAsync(read, member, _ => Task.FromResult(new Seen("suspended")), Cancellation) == new Seen("suspended"));

        rightAfter.ShouldBe(new Seen("active"));
    }

    // A message older than the longest lifetime of an entry has nothing left to remove. It ends at once, with no call to a Valkey that is
    // down, so it is never tried again and never reaches the dead letter queue. A message that still matters calls Valkey, and fails.
    [Fact]
    public async Task RemoveCacheTags_AMessageOlderThanAnEntryLives_EndsWithoutCallingValkey()
    {
        await using var api = Host();
        _ = api.Services;
        await _valkey.StopAsync();
        using var scope = api.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var tags = new[] { CacheTag.Of(CacheLabel.User, "user_old") };

        await bus.InvokeAsync(new RemoveCacheTagsCommand(tags, DateTimeOffset.UtcNow - CachedRead.LongestLifetime - TimeSpan.FromSeconds(1)), Cancellation);
        var stillNeeded = () => api.Services.GetRequiredService<CacheRemoval>().RemoveAsync(tags);

        await stillNeeded.ShouldThrowAsync<Exception>();
    }

    private static async Task<HttpStatusCode> MembersAsync(ApiFactory api, string member, Guid tenantId) =>
        (await api.CreateClient(member).GetAsync($"/v1/tenants/{tenantId}/members", Cancellation)).StatusCode;

    private ApiFactory Host(bool removeAtCommit = true) => new(
        database.ConnectionStringFor(DatabaseRoles.Application, _databaseName),
        settings: new Dictionary<string, string?> { ["ConnectionStrings:Cache"] = _valkey.ConnectionString },
        configureServices: services => services.AddCacheInvalidation(removeAtCommit));

    public sealed record Seen(string State);
}
