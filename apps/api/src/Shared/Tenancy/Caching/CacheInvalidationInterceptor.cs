using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Tenancy.Caching;

// The removal of the cache tags of the rows a module DbContext saved (C6). A save collects them from the module's tag table, and writes
// their durable removal in its transaction. The end of the transaction removes them at once, the fast path, or drops them. Without the
// fast path (removeAtCommit: false), only the durable removal runs, as after a crash between the commit and the removal; a test uses it.
internal sealed class CacheInvalidationInterceptor(ReadCache cache, TimeProvider time, bool removeAtCommit) : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CollectAsync(eventData.Context).AsTask().GetAwaiter().GetResult();
        return result;
    }

    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await CollectAsync(eventData.Context);
        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        RemoveOutsideATransactionAsync(eventData.Context).GetAwaiter().GetResult();
        return result;
    }

    public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await RemoveOutsideATransactionAsync(eventData.Context);
        return result;
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData) => DiscardOutsideATransaction(eventData.Context);

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        DiscardOutsideATransaction(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        RemoveAsync(eventData.Context).GetAwaiter().GetResult();

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
        RemoveAsync(eventData.Context);

    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) =>
        RemoveAsync(eventData.Context).GetAwaiter().GetResult();

    public Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default) =>
        RemoveAsync(eventData.Context);

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        (eventData.Context as TenantDbContext)?.DiscardCacheTags();

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        (eventData.Context as TenantDbContext)?.DiscardCacheTags();
        return Task.CompletedTask;
    }

    private static void DiscardOutsideATransaction(DbContext? context)
    {
        if (context is TenantDbContext { Database.CurrentTransaction: null } module)
        {
            module.DiscardCacheTags();
        }
    }

    private Task RemoveOutsideATransactionAsync(DbContext? context) =>
        context is TenantDbContext { Database.CurrentTransaction: null } ? RemoveAsync(context) : Task.CompletedTask;

    private async ValueTask CollectAsync(DbContext? context)
    {
        if (context is TenantDbContext module)
        {
            module.CollectCacheTags();
            await module.SendCacheRemovalAsync(time.GetUtcNow());
        }
    }

    private Task RemoveAsync(DbContext? context) =>
        context is TenantDbContext module && module.TakeCacheTags() is { Count: > 0 } tags && removeAtCommit ? cache.RemoveTagsAsync(tags) : Task.CompletedTask;
}
