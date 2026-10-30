using StackExchange.Redis;

namespace Api.Caching;

// The one connection of a process to Valkey, opened at its first use: the cache reads through it (C9), and the live channel publishes and
// subscribes on it (U6). It exists only with ConnectionStrings:Cache.
internal sealed class ValkeyConnection(Lazy<Task<IConnectionMultiplexer>> connecting)
{
    public Task<IConnectionMultiplexer> ConnectAsync() => connecting.Value;
}
