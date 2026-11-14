namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>A flag that is on for the organisation, with its type, the first rule that turns it on, and the percentage of rule 2 (4.09).</summary>
public sealed record TenantFeatureFlagResponse(string Key, FeatureFlagResponseType Type, TenantFeatureFlagResponseReason Reason, int Percentage);
