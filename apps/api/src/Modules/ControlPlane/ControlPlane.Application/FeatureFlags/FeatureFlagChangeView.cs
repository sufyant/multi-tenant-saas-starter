using ControlPlane.Application.Users;

namespace ControlPlane.Application.FeatureFlags;

/// <summary>
/// One line of a flag's history: what changed, from what to what, the organisation listed or removed with its name now, the commit of a
/// deploy, when, and the system admin who changed it, or none for a deploy.
/// </summary>
public sealed record FeatureFlagChangeView(
    string Kind,
    string? From,
    string? To,
    FeatureFlagTenantNameView? Tenant,
    string? Revision,
    DateTimeOffset At,
    UserNameView? Actor);
