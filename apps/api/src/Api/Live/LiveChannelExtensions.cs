using Api.Caching;
using SharedKernel;

namespace Api.Live;

internal static class LiveChannelExtensions
{
    // U6: with ConnectionStrings:Cache, every process publishes on Valkey and every web process hears it; without it, the events stay in
    // the process, which is enough in the role all of local development and the tests.
    public static WebApplicationBuilder AddLiveChannel(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ILiveChannel>(provider => new LiveChannel(
            provider.GetRequiredService<ILogger<LiveChannel>>(), provider.GetService<ValkeyConnection>()));
        return builder;
    }
}
