using Api.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tenancy;
using Tenancy.Caching;

namespace Api.IntegrationTests.Fixtures;

// The cache as one process composes it (CachingExtensions), and nothing else of the host. Two of them on one Valkey stand for two
// processes: each has its own FusionCache, and they share only Valkey.
internal static class CacheHosts
{
    // With a database, the host has the probe table as a module DbContext with a tag table (CachedProbeDbContext).
    public static ServiceProvider Start(string? cacheConnection, string? databaseConnection = null)
    {
        var builder = WebApplication.CreateEmptyBuilder(new());
        builder.Configuration[$"ConnectionStrings:{CachingExtensions.CacheConnection}"] = cacheConnection;
        if (Database.ForHost(databaseConnection, nameof(CacheHosts)) is { } database)
        {
            builder.Services.AddTenancy(_ => database);
            builder.Services.AddDbContext<CachedProbeDbContext>((provider, options) => options.UseModuleDatabase(provider, ProbeDbContext.Schema));
        }

        builder.Services.AddFakeLogging(options => options.CollectRecordsForDisabledLogLevels = false);
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug));
        builder.Services.AddMetrics();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.AddCaching();
        return builder.Services.BuildServiceProvider();
    }

    public static ReadCache Cache(this IServiceProvider host) => host.GetRequiredService<ReadCache>();
}
