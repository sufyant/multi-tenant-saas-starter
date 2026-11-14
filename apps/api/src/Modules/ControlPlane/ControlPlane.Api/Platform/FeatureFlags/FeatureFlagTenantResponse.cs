namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>An organisation that the flag's first rule lists, with its name now, and when a system admin listed it.</summary>
public sealed record FeatureFlagTenantResponse(Guid TenantId, string Name, DateTimeOffset ListedAt);
