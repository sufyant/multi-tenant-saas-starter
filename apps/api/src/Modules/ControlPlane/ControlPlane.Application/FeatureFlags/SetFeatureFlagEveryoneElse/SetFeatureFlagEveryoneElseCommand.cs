using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.SetFeatureFlagEveryoneElse;

/// <summary>A system admin turns the flag on or off for everyone else, its third rule (12.10, 12.11).</summary>
[MessageName("set-feature-flag-everyone-else")]
public sealed record SetFeatureFlagEveryoneElseCommand(string ActorId, string Key, bool On);
