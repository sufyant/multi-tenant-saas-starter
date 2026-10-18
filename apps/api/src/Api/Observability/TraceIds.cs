using System.Diagnostics;

namespace Api.Observability;

// The one place that derives the trace id the host names (B25). The formats stay as they were.
internal static class TraceIds
{
    // A Problem Details answer and a saved idempotent result: the id of the current activity, else the request's own identifier.
    public static string Of(HttpContext context) => Activity.Current?.Id ?? context.TraceIdentifier;

    // A log line of the messaging: the trace id alone, as an OpenTelemetry backend shows it.
    public static string? OfCurrentTrace() => Activity.Current?.TraceId.ToString();
}
