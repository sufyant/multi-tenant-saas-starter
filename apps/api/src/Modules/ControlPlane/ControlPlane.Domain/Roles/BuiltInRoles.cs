using System.Collections.Frozen;

namespace ControlPlane.Domain.Roles;

/// <summary>
/// The kinds of role every tenant has, and their permissions. Their permissions are fixed here and cannot be changed. Each tenant has
/// its own row of each kind (A2), which its creation writes; code finds a tenant's role by its kind, never by a fixed id.
/// </summary>
internal static class BuiltInRoles
{
    private static readonly FrozenSet<string> OwnerPermissions = PermissionPools.TenantPool.ToFrozenSet(StringComparer.Ordinal);

    // Admins hold what owners hold, but the permissions that are the owners' alone (K1).
    private static readonly FrozenSet<string> AdminPermissions = PermissionPools.TenantPool.Except(PermissionPools.OwnerOnly).ToFrozenSet(StringComparer.Ordinal);

    // Members get the permissions of the capabilities that modules add for them.
    private static readonly FrozenSet<string> MemberPermissions = FrozenSet.Create(
        StringComparer.Ordinal,
        PermissionPools.TenantMembersRead,
        PermissionPools.TenantProfileRead);

    /// <summary>The built-in roles whose permissions hold the permission.</summary>
    public static IReadOnlyList<BuiltInRole> Holding(string permission) =>
        [.. Enum.GetValues<BuiltInRole>().Where(role => PermissionsOf(role).Contains(permission))];

    public static IReadOnlySet<string> PermissionsOf(BuiltInRole role) => role switch
    {
        BuiltInRole.Owner => OwnerPermissions,
        BuiltInRole.Admin => AdminPermissions,
        BuiltInRole.Member => MemberPermissions,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Every built-in role has its permissions."),
    };
}
