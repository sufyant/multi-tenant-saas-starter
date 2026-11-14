using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.CheckFeatureFlagCleanup;

/// <summary>
/// The worker's daily look at the flags' cleanup (3.07). No endpoint sends it, so it is no command of K3, and it writes no audit entry:
/// it changes no data.
/// </summary>
[MessageName("check-feature-flag-cleanup")]
public sealed record CheckFeatureFlagCleanupCommand;
