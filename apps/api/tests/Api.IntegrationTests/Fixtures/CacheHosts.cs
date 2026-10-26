using Api.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tenancy.Caching;

namespace Api.IntegrationTests.Fixtures;

// The cache as one process composes it (CachingExtensions), and nothing else of the host. Two of them on one Valkey stand for two
// processes: each has its own FusionCache, and they share only Valkey.
internal static class CacheHosts
{
    public static ServiceProvider Start(string? cacheConnection)
    {
        var builder = WebApplication.CreateEmptyBuilder(new());
        builder.Configuration[$"ConnectionStrings:{CachingExtensions.CacheConnection}"] = cacheConnection;
        builder.Services.AddFakeLogging(options => options.CollectRecordsForDisabledLogLevels = false);
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug));
        builder.Services.AddMetrics();
        builder.AddCaching();
        return builder.Services.BuildServiceProvider();
    }

    public static ReadCache Cache(this IServiceProvider host) => host.GetRequiredService<ReadCache>();
}
