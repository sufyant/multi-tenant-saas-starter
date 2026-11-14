using ControlPlane.Api.Tenant.Invitations;

namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>A flag's latest change: when, and the system admin who made it, with their name now; none when a deploy made it.</summary>
public sealed record FeatureFlagLastChangeResponse(DateTimeOffset At, UserNameResponse? Actor);
