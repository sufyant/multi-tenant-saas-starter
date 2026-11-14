namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// The keys of the feature flags that the code defines, declared once, here (D1). Each key has its definition in
/// <see cref="FeatureFlagCatalog.Template"/>. Contracts does not reference Domain (section 2), so
/// <c>apps/api/scripts/generate-feature-flags.mjs</c> writes <c>ControlPlane.Contracts/FeatureFlags/FeatureFlagKeys.g.cs</c> and the web's
/// <c>packages/api-client/src/feature-flags.gen.ts</c> from these constants. A removed flag's key moves to
/// <see cref="FeatureFlagCatalog.Template"/>'s retired keys, and its name is never used again.
/// </summary>
internal static class FeatureFlagKeys
{
}
