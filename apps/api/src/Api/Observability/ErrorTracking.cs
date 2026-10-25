using Sentry.AspNetCore;
using Sentry.Extensibility;

namespace Api.Observability;

// The one place that knows the error tracker's SDK (section 7, "Error tracking"). The rest of the code logs with ILogger and throws as
// before. The SDK speaks the Sentry protocol, so the tracker changes with the DSN alone.
internal static class ErrorTracking
{
    // The settings are read while the host is composed, since the SDK is registered only with a DSN, and checked on start like every
    // other setting. Without a DSN the host has none of the SDK's services: the SDK keeps one hub for the process, and hosts that
    // share a process (as in the tests) must not replace it.
    public static WebApplicationBuilder AddErrorTracking(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<ErrorTrackingOptions>()
            .BindConfiguration(ErrorTrackingOptions.Section)
            .Validate(settings => !settings.IsOn || Uri.TryCreate(settings.Dsn, UriKind.Absolute, out _), $"{ErrorTrackingOptions.Section}:Dsn must be the absolute URL of the error tracker's DSN.")
            .ValidateOnStart();

        var settings = builder.Configuration.GetSection(ErrorTrackingOptions.Section).Get<ErrorTrackingOptions>() ?? new ErrorTrackingOptions();
        // Without a DSN, nothing to register. With a DSN that is no absolute URL, the start check above names the key.
        if (!Uri.TryCreate(settings.Dsn, UriKind.Absolute, out _))
        {
            return builder;
        }

        var environment = builder.Environment.EnvironmentName.ToLowerInvariant();
        builder.WebHost.UseSentry(sentry =>
        {
            sentry.Dsn = settings.Dsn;
            sentry.Release = string.IsNullOrWhiteSpace(settings.Release) ? null : settings.Release;
            sentry.Environment = environment;

            // Errors only: no traces, no sessions, no reports about the SDK itself. Traces go through OpenTelemetry.
            sentry.TracesSampleRate = 0;
            sentry.AutoSessionTracking = false;
            sentry.SendClientReports = false;

            // No personal data: no user, no client address, no cookies, no request body.
            sentry.SendDefaultPii = false;
            sentry.MaxRequestBodySize = RequestSize.None;

            // A call to another service that fails reaches the code as an exception, and the code reports it once.
            sentry.CaptureFailedRequests = false;
            sentry.DisableSentryHttpMessageHandler = true;

            // A log at Error or above is an event; a log below it is a breadcrumb of the next event.
            sentry.MinimumEventLevel = LogLevel.Error;
        });

        return builder;
    }
}
