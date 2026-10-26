using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Tenancy.Caching;

/// <summary>
/// The one way to cache a read (C2), and the one code besides the composition root that calls the
/// cache. A read names its <see cref="CachedRead"/>, which builds the key and the tags; the cache keeps the answer for the read's
/// lifetime. Without a cache, every read goes to the database. A cache that fails or is slow never fails a request: the read goes to the
/// database, and the failure is a warning, because a cache that is down needs no person (section 7, "Error tracking", rule 4).
/// </summary>
/// <remarks>The composition root gives it the cache, or none, and sets the time limit of each call to the cache.</remarks>
public sealed partial class ReadCache(HybridCache? cache, ILogger<ReadCache> logger, IMeterFactory meters)
{
    /// <summary>The meter of the cache's counter, which the host exports.</summary>
    public const string MeterName = "Tenancy.Caching";

    private readonly Counter<long> _reads = meters.Create(MeterName).CreateCounter<long>(
        "cache.reads",
        description: "Cached reads, by the kind of read and the result: a hit, a miss that read the database, or a failure of the cache.");

    public Task<T> ReadAsync<T>(TenantCachedRead<T> read, Guid tenantId, string subject, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken) =>
        ReadAsync(read, read.KeyOf(tenantId, subject), read.TagsOf(tenantId, subject), load, cancellationToken);

    public Task<T> ReadAsync<T>(UserCachedRead<T> read, string externalUserId, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken) =>
        ReadAsync(read, read.KeyOf(externalUserId), read.TagsOf(externalUserId), load, cancellationToken);

    /// <summary>
    /// Removes every entry that names one of the tags, in every process, because they share the cache. It runs after a commit, so a
    /// failure here changes no data: the entries then live until they expire, and the failure is a warning.
    /// </summary>
    public async Task RemoveTagsAsync(IReadOnlyCollection<string> tags)
    {
        if (cache is null || tags.Count == 0)
        {
            return;
        }

        try
        {
            await cache.RemoveByTagAsync(tags, CancellationToken.None);
        }
        catch (Exception exception)
        {
            LogRemovalFailed(logger, exception, string.Join(", ", tags.Select(CacheTag.KindOf).Distinct()));
        }
    }

    [LoggerMessage(EventId = 1, EventName = "CacheReadFailed", Level = LogLevel.Warning, Message = "The cache failed to answer the read {Kind}; the read went to the database")]
    private static partial void LogReadFailed(ILogger logger, Exception exception, string kind);

    [LoggerMessage(EventId = 2, EventName = "CacheRemovalFailed", Level = LogLevel.Warning, Message = "The cache failed to remove the entries of the tags {Kinds} after a commit; they live until they expire")]
    private static partial void LogRemovalFailed(ILogger logger, Exception exception, string kinds);

    private async Task<T> ReadAsync<T>(CachedRead read, string key, IReadOnlyList<string> tags, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken)
    {
        if (cache is null)
        {
            return await load(cancellationToken);
        }

        var miss = new Miss<T>(load, cancellationToken);
        try
        {
            var value = await cache.GetOrCreateAsync(
                key,
                miss,
                static async (state, _) => await state.LoadAsync(),
                new HybridCacheEntryOptions { Expiration = read.Lifetime },
                tags,
                cancellationToken);
            Count(read, miss.Loaded ? "miss" : "hit");
            return value;
        }
        catch (Exception exception) when (miss.Failure is null && !cancellationToken.IsCancellationRequested)
        {
            LogReadFailed(logger, exception, read.Kind);
            Count(read, "failure");
            return miss.Loaded ? miss.Value! : await load(cancellationToken);
        }
        catch when (miss.Failure is not null)
        {
            // The database failed, not the cache: the read fails as it would without a cache.
            ExceptionDispatchInfo.Throw(miss.Failure);
            throw;
        }
    }

    private void Count(CachedRead read, string result) =>
        _reads.Add(1, new KeyValuePair<string, object?>("cache.read", read.Kind), new KeyValuePair<string, object?>("cache.result", result));

    // The database read of a miss, run by the cache at most once. It tells a failure of the database from one of the cache, and keeps a
    // value the cache failed to store, so a failure after the read does not read the database again.
    private sealed class Miss<T>(Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken)
    {
        public bool Loaded { get; private set; }

        public T? Value { get; private set; }

        public Exception? Failure { get; private set; }

        public async ValueTask<T> LoadAsync()
        {
            try
            {
                Value = await load(cancellationToken);
                Loaded = true;
                return Value;
            }
            catch (Exception exception)
            {
                Failure = exception;
                throw;
            }
        }
    }
}
