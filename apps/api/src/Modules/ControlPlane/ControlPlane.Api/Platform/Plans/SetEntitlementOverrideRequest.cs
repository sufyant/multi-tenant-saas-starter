namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// The tenant's value of the key in the route, over its plan's value; its expiry, after now, or null for one that never expires; and the
/// system admin's reason, required. The API trims the reason before it counts its length.
/// </summary>
public sealed record SetEntitlementOverrideRequest(EntitlementValueRequest Value, DateTimeOffset? ExpiresAt, string Reason);
