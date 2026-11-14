namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// What a flag's history says about its removal from the code (Fowler and Hodgson, "Feature Toggles", "Working with feature-flagged
/// systems": every flag carries a cost, so remove it once it has done its work).
/// </summary>
internal enum FeatureFlagCleanup
{
    /// <summary>A release flag that has been on for everyone for <see cref="FeatureFlag.CleanupAfter"/>: its work is done.</summary>
    ReadyToRemove,

    /// <summary>Any flag that has been off for every organisation for <see cref="FeatureFlag.CleanupAfter"/>: nothing uses it.</summary>
    Unused,
}
