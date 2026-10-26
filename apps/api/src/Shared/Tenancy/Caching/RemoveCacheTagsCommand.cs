using SharedKernel;

namespace Tenancy.Caching;

/// <summary>
/// The durable removal of the cache tags of the rows that one transaction saved (C6). The save writes it in the same transaction, to be
/// handled <see cref="CacheRemoval.Delay"/> after the commit, so it arrives at least once, whatever happens to the removal at the commit.
/// </summary>
/// <param name="CommittedAt">When the rows were saved, by the clock of the process that saved them.</param>
[MessageName("remove-cache-tags")]
public sealed record RemoveCacheTagsCommand(IReadOnlyList<string> Tags, DateTimeOffset CommittedAt);
