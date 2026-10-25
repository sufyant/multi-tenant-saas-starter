namespace Api.Observability;

// Where the host sends its errors, and the build they come from (section 7, "Error tracking"). Without a DSN nothing is sent.
internal sealed class ErrorTrackingOptions
{
    public const string Section = "Host:ErrorTracking";

    /// <summary>The error tracker's DSN: the address that takes events in the Sentry protocol, with its key.</summary>
    public string? Dsn { get; set; }

    /// <summary>The commit the image was built from. The image sets it (Dockerfile); each event names it.</summary>
    public string? Release { get; set; }

    public bool IsOn => !string.IsNullOrWhiteSpace(Dsn);
}
