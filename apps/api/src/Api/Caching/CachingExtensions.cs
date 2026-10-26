using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;
using Tenancy;
using Tenancy.Caching;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Api.Caching;

// The composition root of the shared cache (C9, C10): Microsoft's HybridCache abstraction, served by
// FusionCache, with Valkey as its distributed level and the in-memory level off. Every process reads the same entries, so a removal
// in one is seen at once in all of them, with no message between them. Without ConnectionStrings:Cache there is no cache: ReadCache
// then reads the database every time. This file and ReadCache are the only code that knows the cache (CachingTests).
internal static partial class CachingExtensions
{
    public const string CacheConnection = "Cache";

    // Every key of the API starts with it, so another application on the same Valkey cannot collide with it.
    internal const string KeyPrefix = "api:";

    // The name of the FusionCache of the durable removal.
    internal const string RemovalCache = "removal";

    // The time limit of each call to Valkey. A call that takes longer fails, and the read goes to the database (Nygard, Release It!,
    // Chapter 5, Timeouts). A read in Valkey on the private network takes well under a millisecond.
    internal static readonly TimeSpan CallTimeLimit = TimeSpan.FromMilliseconds(250);

    // How long the reads go straight to the database after a call to Valkey fails or times out (C10). A slow Valkey then costs one read the
    // time limit every 10 s in each process, not every read. The removal at the commit skips Valkey for as long; the durable removal does not
    // (C6), and it comes after CacheRemoval.Delay, which is shorter.
    internal static readonly TimeSpan BreakerDuration = TimeSpan.FromSeconds(10);

    // How long a tag's removal time stays in Valkey: a minute longer than an entry lives (CachedRead.LongestLifetime), so it outlives every
    // entry it outdates, also with a small difference of the clocks, and Valkey keeps few of them.
    internal static readonly TimeSpan TagLifetime = CachedRead.LongestLifetime + TimeSpan.FromMinutes(1);

    // The time limit of a connection to Valkey, which the first call waits for. A cache that is down at the start fails each call at once
    // afterwards, while StackExchange.Redis connects again in the background.
    private static readonly TimeSpan ConnectTimeLimit = TimeSpan.FromSeconds(1);

    public static WebApplicationBuilder AddCaching(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IWolverineExtension, CacheRemovalRetries>();
        builder.Services.AddSingleton(provider => new ReadCache(
            provider.GetService<HybridCache>(),
            provider.GetRequiredService<ILogger<ReadCache>>(),
            provider.GetRequiredService<IMeterFactory>()));

        if (builder.Configuration.GetConnectionString(CacheConnection) is not { Length: > 0 } connection)
        {
            // A message from a process with a cache can still arrive here, during a deploy that adds or removes the connection.
            builder.Services.AddSingleton(new CacheRemoval(removals: null));
            return builder;
        }

        // Every module DbContext removes the tags of the rows it saves: at once after the commit, and durably by a message (C6).
        builder.Services.AddCacheInvalidation();

        var valkey = ConfigurationOptions.Parse(connection);
        valkey.AbortOnConnectFail = false;
        valkey.ConnectTimeout = (int)ConnectTimeLimit.TotalMilliseconds;
        valkey.AsyncTimeout = (int)CallTimeLimit.TotalMilliseconds;
        valkey.SyncTimeout = (int)CallTimeLimit.TotalMilliseconds;
        var connecting = new Lazy<Task<IConnectionMultiplexer>>(async () => await ConnectionMultiplexer.ConnectAsync(valkey));
        var distributed = new RedisCache(new RedisCacheOptions { ConnectionMultiplexerFactory = () => connecting.Value });


        // The cache of the reads and of the removal at the commit, with the circuit breaker (C10).
        AddValkeyCache(builder.Services.AddFusionCache(), distributed, BreakerDuration)
            .WithPostSetup((provider, cache) =>
            {
                // While the breaker is open, the removal at the commit skips Valkey, and only the durable removal reaches it, so the logs
                // say when the breaker opens and closes.
                var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CachingExtensions).FullName!);
                cache.Events.Distributed.CircuitBreakerChange += (_, change) => LogBreaker(logger, change.IsClosed);

                // C11: Valkey's own count of the keys it evicted, which step 42 shows. An eviction of a tag's marker is the one way a
                // removal can be undone before its entries expire (C9).
                EvictedKeys.Observe(provider.GetRequiredService<IMeterFactory>(), connecting);
            })
            .AsHybridCache();

        // The cache of the durable removal, on the same Valkey, with no circuit breaker: it calls Valkey every time, and fails when Valkey
        // does not take the removal, so Wolverine tries the message again (C6).
        AddValkeyCache(builder.Services.AddFusionCache(RemovalCache), distributed, TimeSpan.Zero).AsKeyedHybridCacheByCacheName();
        builder.Services.AddSingleton(provider => new CacheRemoval(provider.GetRequiredKeyedService<HybridCache>(RemovalCache)));

        return builder;
    }

    // One FusionCache on Valkey, as both instances need it: one level, a tag's removal time read from Valkey on every read, and every
    // failure thrown to ReadCache or to the handler.
    private static IFusionCacheBuilder AddValkeyCache(IFusionCacheBuilder cache, RedisCache distributed, TimeSpan breaker) =>
        cache
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
                options.ReThrowOriginalExceptions = true;

                // FusionCache does not throw its own timeout, even with ReThrowDistributedCacheExceptions (DistributedCacheAccessor,
                // "exc is not SyntheticTimeoutException"): a tag read that ran into it would read as "never removed". So a tag has no
                // timeout of FusionCache's, and StackExchange.Redis's time limit fails it with an exception.
                options.TagsDefaultEntryOptions.DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan;

                // A tag's removal time outlives every entry it removes, so Valkey's volatile-ttl evicts the entries first (C9).
                options.TagsDefaultEntryOptions.Duration = TagLifetime;
                options.TagsDefaultEntryOptions.DistributedCacheDuration = TagLifetime;

                // After a failure, FusionCache skips Valkey for this long; zero is no breaker. FusionCache's auto-recovery replays a
                // skipped write from the entry in memory, and there is none (AutoRecoveryService.TryProcessItemSetAsync), so the
                // durable removal is what reaches Valkey.
                options.DistributedCacheCircuitBreakerDuration = breaker;

                // The one event handler only logs, so it runs where the event happens, and its warning comes with the failure.
                options.EnableSyncEventHandlersExecution = true;
            })
            .WithDefaultEntryOptions(entry =>
            {
                // One level: the processes keep no copy of an entry in their own memory.
                entry.SkipMemoryCacheRead = true;
                entry.SkipMemoryCacheWrite = true;
                entry.IsFailSafeEnabled = false;
                entry.ReThrowDistributedCacheExceptions = true;

                // An entry that times out is a miss, which reads the database: that is safe, and it opens the breaker.
                entry.DistributedCacheHardTimeout = CallTimeLimit;
                entry.AllowBackgroundDistributedCacheOperations = false;
            })
            // ReadCache logs every failure it sees, without the key. FusionCache's own logs name the key, and would log each failure twice.
            .WithoutLogger()
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithDistributedCache(distributed);

    private static void LogBreaker(ILogger logger, bool closed)
    {
        if (closed)
        {
            LogBreakerClosed(logger);
        }
        else
        {
            LogBreakerOpened(logger, BreakerDuration);
        }
    }

    [LoggerMessage(EventId = 3, EventName = "CacheBreakerOpened", Level = LogLevel.Warning, Message = "A call to the cache failed; the reads go to the database for {Duration}, and only the durable removals reach it")]
    private static partial void LogBreakerOpened(ILogger logger, TimeSpan duration);

    [LoggerMessage(EventId = 4, EventName = "CacheBreakerClosed", Level = LogLevel.Information, Message = "The cache answers again; the reads use it")]
    private static partial void LogBreakerClosed(ILogger logger);
}
