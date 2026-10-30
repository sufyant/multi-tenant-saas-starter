using Api.Hosting;

namespace Api.Live;

// U6: one warning at the start of a web or a worker process without ConnectionStrings:Cache. Such a deployment loses every live signal
// of the worker, and nothing else would tell.
internal sealed partial class LiveChannelWithoutCache(ILogger<LiveChannel> logger, HostRole role) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogWithoutCache(logger, role);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The role {Role} runs without ConnectionStrings:Cache: the live signals of the notifications will not reach the web hosts' streams.")]
    private static partial void LogWithoutCache(ILogger logger, HostRole role);
}
