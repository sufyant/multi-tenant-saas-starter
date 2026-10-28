namespace ControlPlane.Domain.Plans;

/// <summary>
/// The keys of the features a plan turns on or off, declared once, here (D1). A key is lowercase and dotted, its area first, as a
/// permission is. A stored key is never renamed; removing one is a migration that deletes its rows. Contracts does not reference Domain
/// (section 2), so <c>apps/api/scripts/generate-entitlements.mjs</c> writes <c>ControlPlane.Contracts/Plans/Features.g.cs</c> from these
/// constants.
/// </summary>
internal static class FeatureKeys
{
    /// <summary>The tenant builds its own roles and gives them (A3).</summary>
    public const string RolesCustom = "roles.custom";
}
