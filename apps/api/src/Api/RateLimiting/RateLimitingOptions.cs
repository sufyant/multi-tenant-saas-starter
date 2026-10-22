namespace Api.RateLimiting;

internal sealed class RateLimitingOptions
{
    public const string Section = "Host:RateLimiting";

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
