using Api.Caching;
using Api.Hosting;
using SharedKernel;

namespace Api.Live;

internal static class LiveChannelExtensions
{
    // U6: with ConnectionStrings:Cache, every process publishes on Valkey and every web process hears it; without it, the events stay in
    // the process, which is enough in the role all of local development and the tests. A web or a worker process without it says so once at
    // start: the worker's signals cannot reach the streams of a web process then.
    public static WebApplicationBuilder AddLiveChannel(this WebApplicationBuilder builder, HostRole? role)
    {
        builder.Services.AddSingleton<ILiveChannel>(provider => new LiveChannel(
            provider.GetRequiredService<ILogger<LiveChannel>>(), provider.GetService<ValkeyConnection>()));
        if (role is HostRole.Web or HostRole.Worker && builder.Configuration.GetConnectionString(CachingExtensions.CacheConnection) is not { Length: > 0 })
        {
            builder.Services.AddHostedService(provider => new LiveChannelWithoutCache(provider.GetRequiredService<ILogger<LiveChannel>>(), role.Value));
        }

        return builder;
    }
}
