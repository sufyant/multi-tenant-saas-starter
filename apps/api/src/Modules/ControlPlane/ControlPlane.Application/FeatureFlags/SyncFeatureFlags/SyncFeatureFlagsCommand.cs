using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.SyncFeatureFlags;

/// <summary>
/// The worker's comparison of the code's flags with the stored ones, at its start (docs/flows/feature-flags.md). No endpoint sends it, so it
/// is no command of K3; the flags it adds or archives write their audit entries all the same, by the platform.
/// </summary>
[MessageName("sync-feature-flags")]
public sealed record SyncFeatureFlagsCommand;
