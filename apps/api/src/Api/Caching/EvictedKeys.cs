using System.Diagnostics.Metrics;
using System.Globalization;
using StackExchange.Redis;
using Tenancy.Caching;

namespace Api.Caching;

// The gauge cache.evicted_keys on the cache's meter: Valkey's evicted_keys (INFO stats), read when the meter is read (C11). It reads only
// when the connection is up and answers within its time limit; otherwise it reports nothing, and the read fails no request.
internal static class EvictedKeys
{
    // The meter keeps the gauge, and with it the callback.
    public static void Observe(IMeterFactory meters, Lazy<Task<IConnectionMultiplexer>> connecting) =>
        meters.Create(ReadCache.MeterName).CreateObservableGauge(
            "cache.evicted_keys",
            () => Read(connecting),
            description: "The keys that Valkey evicted since it started, because its memory was full.");

    private static List<Measurement<long>> Read(Lazy<Task<IConnectionMultiplexer>> connecting)
    {
        if (!connecting.IsValueCreated || connecting.Value is not { IsCompletedSuccessfully: true } connected || !connected.Result.IsConnected)
        {
            return [];
        }

        try
        {
            // INFO through Execute: the client's admin mode stays off, so it sends no other server command.
            var stats = (string)connected.Result.GetDatabase().Execute("INFO", "stats")!;
            return [.. stats.Split('\n')
                .Select(line => line.Trim().Split(':'))
                .Where(pair => pair is ["evicted_keys", _])
                .Select(pair => new Measurement<long>(long.Parse(pair[1], CultureInfo.InvariantCulture)))
                .Take(1)];
        }
        catch (Exception exception) when (exception is RedisException or RedisCommandException)
        {
            return [];
        }
    }
}
