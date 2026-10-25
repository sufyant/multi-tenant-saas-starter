using Api.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Api.Observability;

internal static class HeartbeatExtensions
{
    // The settings are read while the host is composed, since the worker runs the heartbeat only with a URL, and checked on start like
    // every other setting.
    public static WebApplicationBuilder AddHeartbeat(this WebApplicationBuilder builder, HostRole? role)
    {
        builder.Services.AddOptions<HeartbeatOptions>()
            .BindConfiguration(HeartbeatOptions.Section)
            .Validate(settings => !settings.IsOn || IsHttpUrl(settings.Url), $"{HeartbeatOptions.Section}:Url must be an absolute http or https URL.")
            .Validate(settings => settings.Interval > TimeSpan.Zero, $"{HeartbeatOptions.Section}:Interval must be positive.")
            .ValidateOnStart();

        var settings = builder.Configuration.GetSection(HeartbeatOptions.Section).Get<HeartbeatOptions>() ?? new HeartbeatOptions();

        // The web host's uptime monitor calls a public route; the worker answers none, so its heartbeat is its only alert when it stops.
        if (role is not HostRole.Worker || !IsHttpUrl(settings.Url))
        {
            return builder;
        }

        // The token is in the URL's path, and the HTTP client's logs redact only the query, so the client writes no log.
        builder.Services.AddHttpClient(Heartbeat.Client, http => http.Timeout = Heartbeat.Timeout).RemoveAllLoggers();
        builder.Services.AddHostedService<Heartbeat>();

        return builder;
    }

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var address) && (address.Scheme == Uri.UriSchemeHttps || address.Scheme == Uri.UriSchemeHttp);
}

// Calls the heartbeat URL once per interval while the worker is ready: its readiness checks pass, the database included. A worker
// that runs but cannot work sends nothing, and the missing call is the alert (section 7, "Heartbeat").
internal sealed partial class Heartbeat(HealthCheckService health, IHttpClientFactory clients, IOptions<HeartbeatOptions> settings, TimeProvider time, ILogger<Heartbeat> logger)
    : BackgroundService
{
    public const string Client = "Heartbeat";

    // Every external call has a time limit (Nygard, Chapter 5). The next interval tries again.
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly PeriodicTimer _timer = new(settings.Value.Interval, time);

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            var readiness = await health.CheckHealthAsync(check => check.Tags.Contains(ApiPipeline.ReadyTag), stoppingToken);
            if (readiness.Status != HealthStatus.Unhealthy)
            {
                await BeatAsync(stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The heartbeat call was answered {Status}")]
    private static partial void LogRefused(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The heartbeat call failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);

    // A failed call is a Warning: the missing heartbeat is the alert, and an Error would be a second one. No message names the URL.
    private async Task BeatAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var response = await clients.CreateClient(Client).GetAsync(new Uri(settings.Value.Url!), stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                LogRefused(logger, (int)response.StatusCode);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException && !stoppingToken.IsCancellationRequested)
        {
            LogFailed(logger, exception.GetType().Name);
        }
    }
}
