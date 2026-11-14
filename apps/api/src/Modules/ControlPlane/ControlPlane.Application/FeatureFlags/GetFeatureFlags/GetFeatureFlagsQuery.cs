using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.GetFeatureFlags;

/// <summary>Every flag of this environment, for the list and its tabs (12.01 to 12.04). Flags are few, so the read has no page.</summary>
[MessageName("get-feature-flags")]
public sealed record GetFeatureFlagsQuery;
