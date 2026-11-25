namespace ControlPlane.Domain.Plans;

/// <summary>
/// The keys of the product's limits, beside the template's in <see cref="LimitKeys"/> (X1), with the same rules. A limit may depend on a
/// feature of <see cref="FeatureKeys"/> or <see cref="ProductFeatureKeys"/>. The template ships this class empty and never changes it: a
/// product declares its limits here, and <c>apps/api/scripts/generate-entitlements.mjs</c> writes them into
/// <c>ControlPlane.Contracts/Plans/Limits.g.cs</c>. The product's module checks its limit in its own method, under its own lock, with
/// the tenant's effective value read from the database, not the cache (E6, X3).
/// </summary>
internal static class ProductLimitKeys
{
    /// <summary>How many projects the tenant may have.</summary>
    [DependsOn(ProductFeatureKeys.ProjectsAccess)]
    public const string ProjectsMax = "projects.max";
}
