namespace Api.RateLimiting;

internal sealed class RateLimitingOptions
{
    public const string Section = "Host:RateLimiting";

    /// <summary>Whether the pipeline runs the rate limiter. Only test hosts turn it off, and only in development (Deviations, "Rate limiting off in test hosts").</summary>
    public bool Enabled { get; set; } = true;

    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }

    /// <summary>The stricter limit of accepting an invitation, on top of the general one (API4).</summary>
    public WindowLimitOptions InvitationAccept { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };
}

internal sealed class WindowLimitOptions
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }
}
