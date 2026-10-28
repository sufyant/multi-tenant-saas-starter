namespace ControlPlane.Domain.Plans;

/// <summary>
/// The keys of the limits a plan sets, a number or unlimited, declared once, here (D1), with the rules of <see cref="FeatureKeys"/>.
/// <c>apps/api/scripts/generate-entitlements.mjs</c> writes <c>ControlPlane.Contracts/Plans/Limits.g.cs</c> from these constants.
/// </summary>
internal static class LimitKeys
{
    /// <summary>How many custom roles the tenant may have. The built-in roles do not count.</summary>
    public const string RolesCustomMax = "roles.custom.max";

    /// <summary>How many members the tenant may have. Its pending invitations that have not expired count too.</summary>
    public const string MembersMax = "members.max";
}
