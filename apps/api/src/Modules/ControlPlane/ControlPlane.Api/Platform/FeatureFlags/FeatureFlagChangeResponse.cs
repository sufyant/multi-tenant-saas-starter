using ControlPlane.Api.Tenant.Invitations;

namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>
/// One line of a flag's history. <c>from</c> and <c>to</c> hold a percentage's digits, or <c>on</c> and <c>off</c> for everyone else;
/// <c>tenant</c> the organisation listed or removed, with its name now; <c>revision</c> the commit of the deploy that added or removed the
/// flag. <c>actor</c> is the system admin, with their name now, and none for a deploy.
/// </summary>
public sealed record FeatureFlagChangeResponse(
    FeatureFlagChangeResponseKind Kind,
    string? From,
    string? To,
    FeatureFlagChangeTenantResponse? Tenant,
    string? Revision,
    DateTimeOffset At,
    UserNameResponse? Actor);
