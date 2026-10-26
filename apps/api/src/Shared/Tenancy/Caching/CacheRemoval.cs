using Microsoft.Extensions.Caching.Hybrid;

namespace Tenancy.Caching;

/// <summary>
/// The durable removal of cache tags (C6): the handler of <see cref="RemoveCacheTagsCommand"/> calls it. Unlike the removal at the commit,
/// it skips no call while the cache's circuit breaker is open, and it throws when Valkey does not take the removal, so the message is tried
/// again. The composition root gives it a cache of its own for this, or none.
/// </summary>
public sealed class CacheRemoval(HybridCache? removals)
{
    /// <summary>
    /// How long after the commit the message removes the tags: long enough to also remove an entry that a read which began before the
    /// commit wrote after the removal at the commit, and to cover the clocks of two processes that differ a little (a delayed double delete).
    /// </summary>
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Whether a removal saved at that moment can still find an entry: an entry lives <see cref="CachedRead.LongestLifetime"/> at most, so
    /// a message older than that has nothing left to remove.
    /// </summary>
    public static bool IsStillNeeded(DateTimeOffset committedAt, DateTimeOffset now) => now - committedAt < CachedRead.LongestLifetime;

    /// <summary>Removes the tags, or throws when Valkey does not take the removal.</summary>
    public async Task RemoveAsync(IReadOnlyCollection<string> tags)
    {
        if (removals is not null && tags.Count > 0)
        {
            await removals.RemoveByTagAsync(tags, CancellationToken.None);
        }
    }
}
