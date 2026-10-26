using Tenancy.Caching;

namespace Api.Caching;

// C6: the durable removal of the cache tags that a commit wrote. Valkey down or slow fails it, and Wolverine tries it again
// (CacheRemovalRetries). A message older than the longest lifetime of an entry has nothing left to remove: it ends with no call, so its
// last try never reaches the dead letter queue and raises no alert.
public static class RemoveCacheTagsHandler
{
    public static async Task HandleAsync(RemoveCacheTagsCommand command, CacheRemoval removal, TimeProvider time)
    {
        if (CacheRemoval.IsStillNeeded(command.CommittedAt, time.GetUtcNow()))
        {
            await removal.RemoveAsync(command.Tags);
        }
    }
}
