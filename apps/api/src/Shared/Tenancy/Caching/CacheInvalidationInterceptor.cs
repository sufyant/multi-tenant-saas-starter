using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Tenancy.Caching;

// Removes the cache entries of the rows a module DbContext changed, after the commit (ARCHITECTURE.md, section 3, "Cache"). No handler
// and no command calls the cache: the save collects the tags of the rows it writes from the module's tag table, and the commit of the
// transaction removes them. Before the commit, another request could read the old row and put it back. A save outside a transaction
// commits by itself, so it removes its tags when it has saved; a transaction that rolls back removes nothing. A commit that fails may
// still have committed, so its tags go too: a removal is never wrong.
internal sealed class CacheInvalidationInterceptor(ReadCache cache) : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        (eventData.Context as TenantDbContext)?.CollectCacheTags();
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        (eventData.Context as TenantDbContext)?.CollectCacheTags();
        return ValueTask.FromResult(result);
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

    private Task RemoveAsync(DbContext? context) =>
        context is TenantDbContext module ? cache.RemoveTagsAsync(module.TakeCacheTags()) : Task.CompletedTask;
}
