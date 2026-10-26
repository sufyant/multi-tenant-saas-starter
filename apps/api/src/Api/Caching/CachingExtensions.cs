using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;
using Tenancy.Caching;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Api.Caching;

// The composition root of the shared cache (ARCHITECTURE.md, section 3, "Cache"): Microsoft's HybridCache abstraction, served by
// FusionCache, with Valkey as its distributed level and the in-memory level off. Every process reads the same entries, so a removal
// in one is seen at once in all of them, with no message between them. Without ConnectionStrings:Cache there is no cache: ReadCache
// then reads the database every time. This file and ReadCache are the only code that knows the cache (CachingTests).
internal static class CachingExtensions
{
    public const string CacheConnection = "Cache";

    // Every key of the API starts with it, so another application on the same Valkey cannot collide with it.
    internal const string KeyPrefix = "api:";

    // The time limit of each call to Valkey. A call that takes longer fails, and the read goes to the database (Nygard, Release It!,
    // Chapter 5, Timeouts). A read in Valkey on the private network takes well under a millisecond.
    internal static readonly TimeSpan CallTimeLimit = TimeSpan.FromMilliseconds(250);

    // The time limit of a connection to Valkey, which the first call waits for. A cache that is down at the start fails each call at once
    // afterwards, while StackExchange.Redis connects again in the background.
    private static readonly TimeSpan ConnectTimeLimit = TimeSpan.FromSeconds(1);

    public static WebApplicationBuilder AddCaching(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(provider => new ReadCache(provider.GetService<HybridCache>(), provider.GetRequiredService<ILogger<ReadCache>>()));

        if (builder.Configuration.GetConnectionString(CacheConnection) is not { Length: > 0 } connection)
        {
            return builder;
        }

        var valkey = ConfigurationOptions.Parse(connection);
        valkey.AbortOnConnectFail = false;
        valkey.ConnectTimeout = (int)ConnectTimeLimit.TotalMilliseconds;
        valkey.AsyncTimeout = (int)CallTimeLimit.TotalMilliseconds;
        valkey.SyncTimeout = (int)CallTimeLimit.TotalMilliseconds;

        builder.Services.AddFusionCache()
            .WithCacheKeyPrefix(KeyPrefix)
            .WithOptions(options =>
            {
                // A tag's removal time is read from Valkey on every read, never kept in the process, so a removal in another process
                // counts at once. A failure to read it fails the read, which then goes to the database: an entry is never taken as
                // valid because its tag could not be read.
                options.TagsDefaultEntryOptions.SkipMemoryCacheRead = true;
                options.TagsDefaultEntryOptions.SkipMemoryCacheWrite = true;
                options.TagsDefaultEntryOptions.IsFailSafeEnabled = false;
                options.TagsDefaultEntryOptions.ReThrowDistributedCacheExceptions = true;
                options.TagsDefaultEntryOptions.DistributedCacheHardTimeout = CallTimeLimit;
                options.ReThrowOriginalExceptions = true;
            })
            .WithDefaultEntryOptions(entry =>
            {
                // One level: the processes keep no copy of an entry in their own memory.
                entry.SkipMemoryCacheRead = true;
                entry.SkipMemoryCacheWrite = true;
                entry.IsFailSafeEnabled = false;
                entry.ReThrowDistributedCacheExceptions = true;
                entry.DistributedCacheHardTimeout = CallTimeLimit;
                entry.AllowBackgroundDistributedCacheOperations = false;
            })
            // ReadCache logs every failure it sees, without the key. FusionCache's own logs name the key, and would log each failure twice.
            .WithoutLogger()
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithDistributedCache(new RedisCache(new RedisCacheOptions { ConfigurationOptions = valkey }))
            .AsHybridCache();

        return builder;
    }
}
