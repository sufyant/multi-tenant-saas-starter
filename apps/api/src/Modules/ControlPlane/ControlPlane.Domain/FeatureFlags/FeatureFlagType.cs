namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// What a feature flag is for (Fowler and Hodgson, "Feature Toggles"). A release flag hides unfinished work until it is on for everyone,
/// and then leaves the code. A kill switch turns working behaviour off in an incident, and stays in the code.
/// </summary>
internal enum FeatureFlagType
{
    /// <summary>Off by default; it is ready to remove once it is on for everyone.</summary>
    Release,

    /// <summary>On by default; turning it off for everyone stops the behaviour it guards.</summary>
    KillSwitch,
}
