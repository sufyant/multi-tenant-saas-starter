namespace ControlPlane.Application.FeatureFlags;

/// <summary>An organisation that a flag's first rule lists, with its name now, and when a system admin listed it.</summary>
public sealed record FeatureFlagTenantView(Guid TenantId, string Name, DateTimeOffset ListedAt);
