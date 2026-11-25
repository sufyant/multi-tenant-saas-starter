namespace ControlPlane.Domain.Plans;

/// <summary>
/// The keys of the product's features, beside the template's in <see cref="FeatureKeys"/> (X1), with the same rules. The template ships
/// this class empty and never changes it: a product declares its features here, and <c>apps/api/scripts/generate-entitlements.mjs</c>
/// writes them into <c>ControlPlane.Contracts/Plans/Features.g.cs</c>.
/// </summary>
internal static class ProductFeatureKeys
{
    /// <summary>The tenant keeps projects.</summary>
    public const string Projects = "projects";
}
