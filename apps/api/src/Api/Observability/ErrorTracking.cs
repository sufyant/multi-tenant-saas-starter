using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Extensions.Logging;

namespace Api.Observability;

// The one place that knows the error tracker's SDK (section 7, "Error tracking"). The rest of the code logs with ILogger and throws as
// before. The SDK speaks the Sentry protocol, so the tracker changes with the DSN alone.
internal static class ErrorTracking
{
    // The SDK's name of an exception that ASP.NET Core's exception handler caught (SentryMiddleware).
    private const string ExceptionHandlerMechanism = "IExceptionHandlerFeature";

    // Wolverine's log of a message it puts back or schedules again (WolverineRuntime.RescheduledEventId).
    private const string WolverineRuntimeCategory = "Wolverine.Runtime.WolverineRuntime";

    private const int WolverineRescheduledEventId = 109;

    private const string EfCoreCommandErrorEventName = "Microsoft.EntityFrameworkCore.Database.Command.CommandError";

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

            // Two logs at Error that report no failure of their own. Wolverine logs each put-back and each scheduled retry of a message at
            // Error, and its level cannot be changed; the move to the dead letter queue is the alert. EF Core logs a command the database
            // refused, then throws an exception around the database's to the code, which reports it; the SDK leaves out EF Core's other
            // such logs itself (SentryLogger).
            sentry.AddLogEntryFilter((category, _, eventId, _) =>
                (category == WolverineRuntimeCategory && eventId.Id == WolverineRescheduledEventId)
                || eventId.Name == EfCoreCommandErrorEventName);
        });

        // The exception handler answers some exceptions with a 4xx, such as an invalid command (400); the caller caused them, so they are
        // no event. The SDK reports every exception the handler caught, so the answer's status decides.
        builder.Services.AddOptions<SentryAspNetCoreOptions>().Configure<IHttpContextAccessor>((sentry, http) =>
            sentry.SetBeforeSend((sentryEvent, _) =>
                IsAnsweredBelow500(sentryEvent, http) ? null : sentryEvent));

        return builder;
    }

    private static bool IsAnsweredBelow500(Sentry.SentryEvent sentryEvent, IHttpContextAccessor http) =>
        sentryEvent.SentryExceptions?.Any(exception => exception.Mechanism?.Type == ExceptionHandlerMechanism) == true
        && http.HttpContext?.Response.StatusCode < StatusCodes.Status500InternalServerError;
}
