using ControlPlane.Domain.FeatureFlags;

namespace ControlPlane.Application.FeatureFlags;

/// <summary>
/// The catalog of flags that the running code defines, as a service, so the sync and the evaluation read the one catalog the host
/// registers: the template's, or a test's.
/// </summary>
/// <remarks>Public because a public handler takes it (W9); its catalog speaks domain types, so it is internal to the module.</remarks>
public sealed class FeatureFlagCode
{
    internal FeatureFlagCode(FeatureFlagCatalog catalog) => Catalog = catalog;

    /// <summary>The template's flags.</summary>
    public static FeatureFlagCode Template { get; } = new(FeatureFlagCatalog.Template);

    internal FeatureFlagCatalog Catalog { get; }
}
