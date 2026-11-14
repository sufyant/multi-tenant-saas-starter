namespace ControlPlane.Application.FeatureFlags;

/// <summary>The settings of the feature flags (docs/configuration.md).</summary>
public sealed class FeatureFlagOptions
{
    public const string Section = "ControlPlane:FeatureFlags";

    /// <summary>
    /// The commit the running image was built from, which the sync writes beside a flag it adds or archives. The container image sets it from
    /// its build argument <c>SOURCE_REVISION</c>, so an environment does not set it; without it, the sync writes none.
    /// </summary>
    public string? Revision { get; set; }
}
