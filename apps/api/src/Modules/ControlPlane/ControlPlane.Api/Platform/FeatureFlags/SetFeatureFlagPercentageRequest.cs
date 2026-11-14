namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>The percentage of the flag's second rule: a whole percentage of the organisations; none turns the rule off.</summary>
public sealed record SetFeatureFlagPercentageRequest(int Percentage);
