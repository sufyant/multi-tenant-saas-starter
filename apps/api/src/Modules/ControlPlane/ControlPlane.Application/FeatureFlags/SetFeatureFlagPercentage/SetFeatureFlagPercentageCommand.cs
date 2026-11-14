using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.SetFeatureFlagPercentage;

/// <summary>A system admin sets the percentage of the flag's second rule, from 0 to 100 (12.06).</summary>
[MessageName("set-feature-flag-percentage")]
public sealed record SetFeatureFlagPercentageCommand(string ActorId, string Key, int Percentage);
