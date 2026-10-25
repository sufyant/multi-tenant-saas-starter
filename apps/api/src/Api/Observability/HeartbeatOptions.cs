namespace Api.Observability;

// The worker's heartbeat (section 7, "Heartbeat"). Without a URL nothing runs.
internal sealed class HeartbeatOptions
{
    public const string Section = "Host:Heartbeat";

    /// <summary>The URL the worker calls while it is ready. Its token is in the path, so the URL is a secret.</summary>
    public string? Url { get; set; }

    /// <summary>The time between two calls. The heartbeat's period holds at least two, so one lost call never alerts.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    public bool IsOn => !string.IsNullOrWhiteSpace(Url);
}
