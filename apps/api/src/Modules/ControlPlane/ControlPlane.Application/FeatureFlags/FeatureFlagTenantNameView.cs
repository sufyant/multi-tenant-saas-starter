namespace ControlPlane.Application.FeatureFlags;

/// <summary>An organisation that a line of a flag's history names, with its name now.</summary>
public sealed record FeatureFlagTenantNameView(Guid TenantId, string Name);
