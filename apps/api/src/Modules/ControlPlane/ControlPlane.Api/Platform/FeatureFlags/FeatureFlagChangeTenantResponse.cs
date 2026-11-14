namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>The organisation that a line of a flag's history names, with its name now.</summary>
public sealed record FeatureFlagChangeTenantResponse(Guid Id, string Name);
