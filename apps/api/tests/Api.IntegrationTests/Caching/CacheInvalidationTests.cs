using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenancy.Caching;

namespace Api.IntegrationTests.Caching;

// Section 3, "Cache": the persistence layer removes the tags of every row a save changes, after the commit, and no caller removes an
// entry by hand. Before the commit, a read sees the old row; an entry it leaves then goes with the commit. A rollback removes nothing.
public sealed class CacheInvalidationTests(Valkey valkey, Database database) : IClassFixture<Valkey>
{
    private static readonly UserCachedRead<ProbeCount> CountRead =
        new("test-probe-count", TimeSpan.FromMinutes(1), value => [CacheTag.Of(CachedProbeDbContext.Tag, value)]);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Commit_ASavedRowOfACachedType_RemovesTheEntriesOfItsTag()
    {
        await using var host = CacheHosts.Start(valkey.ConnectionString, database.ApplicationConnectionString);
        var (tenantId, value) = (Guid.NewGuid(), Guid.NewGuid().ToString());
        await host.Cache().ReadAsync(CountRead, value, token => CountAsync(host, tenantId, value, token), Cancellation);

        await using var writer = await BeginAsync(host, tenantId);
        writer.Context.Probes.Add(new Probe { Value = value });
        await writer.Context.SaveChangesAsync(Cancellation);
        await writer.Transaction.CommitAsync(Cancellation);
        var afterCommit = await host.Cache().ReadAsync(CountRead, value, token => CountAsync(host, tenantId, value, token), Cancellation);

        afterCommit.ShouldBe(new ProbeCount(1));
    }

    // A read between the save and the commit sees the committed rows only, and its answer goes to the cache. The removal comes after the
    // commit, so that answer cannot outlive it.
    [Fact]
    public async Task Commit_AReadBetweenTheSaveAndTheCommit_LeavesNoOldAnswerAfterTheCommit()
    {
        await using var host = CacheHosts.Start(valkey.ConnectionString, database.ApplicationConnectionString);
        var (tenantId, value) = (Guid.NewGuid(), Guid.NewGuid().ToString());
        await using var writer = await BeginAsync(host, tenantId);
        writer.Context.Probes.Add(new Probe { Value = value });
        await writer.Context.SaveChangesAsync(Cancellation);

        var beforeCommit = await host.Cache().ReadAsync(CountRead, value, token => CountAsync(host, tenantId, value, token), Cancellation);
        await writer.Transaction.CommitAsync(Cancellation);
        var afterCommit = await host.Cache().ReadAsync(CountRead, value, token => CountAsync(host, tenantId, value, token), Cancellation);

        beforeCommit.ShouldBe(new ProbeCount(0));
        afterCommit.ShouldBe(new ProbeCount(1));
    }

    [Fact]
    public async Task Rollback_ASavedRowOfACachedType_RemovesNothing()
    {
        await using var host = CacheHosts.Start(valkey.ConnectionString, database.ApplicationConnectionString);
        var (tenantId, value) = (Guid.NewGuid(), Guid.NewGuid().ToString());
        var loads = 0;
        await host.Cache().ReadAsync(CountRead, value, token => CountingAsync(), Cancellation);

        await using var writer = await BeginAsync(host, tenantId);
        writer.Context.Probes.Add(new Probe { Value = value });
        await writer.Context.SaveChangesAsync(Cancellation);
        await writer.Transaction.RollbackAsync(Cancellation);
        await host.Cache().ReadAsync(CountRead, value, token => CountingAsync(), Cancellation);

        loads.ShouldBe(1);

        Task<ProbeCount> CountingAsync() => Task.FromResult(new ProbeCount(Interlocked.Increment(ref loads)));
    }

    // A transaction of the tenant on a context of its own, which the test commits or rolls back.
    private static async Task<Writer> BeginAsync(IServiceProvider host, Guid tenantId)
    {
        var scope = host.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CachedProbeDbContext>();
        var transaction = await context.Database.BeginTransactionAsync(Cancellation);
        await context.DeclareTenantAsync(tenantId, Cancellation);
        return new Writer(scope, context, transaction);
    }

    // The database read of the cached answer, in a transaction of its own, which sees only committed rows.
    private static async Task<ProbeCount> CountAsync(IServiceProvider host, Guid tenantId, string value, CancellationToken cancellationToken)
    {
        await using var reader = await BeginAsync(host, tenantId);
        var count = await reader.Context.Probes.CountAsync(probe => probe.Value == value, cancellationToken);
        await reader.Transaction.CommitAsync(cancellationToken);
        return new ProbeCount(count);
    }

    public sealed record ProbeCount(int Count);

    private sealed record Writer(AsyncServiceScope Scope, CachedProbeDbContext Context, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction Transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Transaction.DisposeAsync();
            await Scope.DisposeAsync();
        }
    }
}
