namespace ControlPlane.Api.Tenant.FeatureFlags;

/// <summary>Every flag the code defines, by its key, with whether it is on for the organisation.</summary>
public sealed record MyFeatureFlagsResponse(IReadOnlyDictionary<string, bool> Flags);
