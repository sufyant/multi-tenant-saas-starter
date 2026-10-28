namespace ControlPlane.Domain.Plans;

/// <summary>An override's value for one key and when it expires, if it does: what the effective value reads of an override.</summary>
internal sealed record OverrideValue(string Key, EntitlementValue Value, DateTimeOffset? ExpiresAt)
{
    /// <summary>Whether the override applies at this moment: until its expiry, and always without one. It stops at the expiry itself.</summary>
    public bool IsActiveAt(DateTimeOffset now) => ExpiresAt is not { } expiresAt || now < expiresAt;
}
