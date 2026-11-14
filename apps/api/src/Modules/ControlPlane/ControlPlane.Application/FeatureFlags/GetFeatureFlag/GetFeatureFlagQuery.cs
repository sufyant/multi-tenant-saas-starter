using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.GetFeatureFlag;

/// <summary>One flag by its key, an archived one too, with its rules and its history.</summary>
[MessageName("get-feature-flag")]
public sealed record GetFeatureFlagQuery(string Key);
