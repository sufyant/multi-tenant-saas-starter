using System.Collections.Frozen;

namespace ControlPlane.Domain.Roles;

/// <summary>
/// The kinds of role every tenant has, and their permissions. Their permissions are fixed here and cannot be changed. Each tenant has
/// its own row of each kind (A2), which its creation writes; code finds a tenant's role by its kind, never by a fixed id.
/// </summary>
internal static class BuiltInRoles
{
    private static readonly FrozenSet<string> OwnerPermissions = PermissionPools.TenantPool.ToFrozenSet(StringComparer.Ordinal);

    // Of the built-in roles, only the Owner creates, changes and deletes a custom role. These are not owner-only: an Owner may give them
    // to a custom role (the maintainer's decision 1 in step r4b).
    private static readonly FrozenSet<string> RoleManagement = FrozenSet.Create(
        StringComparer.Ordinal,
        PermissionPools.TenantRolesCreate,
        PermissionPools.TenantRolesUpdate,
        PermissionPools.TenantRolesDelete);

    // Admins hold what owners hold, but the permissions that are the owners' alone (K1), the management of roles and billing. They read
    // the roles, because they give them. Billing is the Owner's and the Billing manager's (step 40, frame 14.01).
    private static readonly FrozenSet<string> AdminPermissions =
        PermissionPools.TenantPool.Except(PermissionPools.OwnerOnly).Except(RoleManagement).Except([PermissionPools.TenantBillingManage])
            .ToFrozenSet(StringComparer.Ordinal);

    // Members get the permissions of the capabilities that modules add for them.
    private static readonly FrozenSet<string> MemberPermissions = FrozenSet.Create(
        StringComparer.Ordinal,
        PermissionPools.TenantMembersRead,
        PermissionPools.TenantProfileRead);

    // A Billing manager reads the tenant and its members, and manages billing. A product module never adds a permission to it, so it
    // never uses the product: it is free and takes no seat (Role.TakesSeat).
    private static readonly FrozenSet<string> BillingManagerPermissions = FrozenSet.Create(
        StringComparer.Ordinal,
        PermissionPools.TenantMembersRead,
        PermissionPools.TenantProfileRead,
        PermissionPools.TenantBillingManage);

    /// <summary>The built-in roles whose permissions hold the permission.</summary>
    public static IReadOnlyList<BuiltInRole> Holding(string permission) =>
        [.. Enum.GetValues<BuiltInRole>().Where(role => PermissionsOf(role).Contains(permission))];

    public static IReadOnlySet<string> PermissionsOf(BuiltInRole role) => role switch
    {
        BuiltInRole.Owner => OwnerPermissions,
        BuiltInRole.Admin => AdminPermissions,
        BuiltInRole.Member => MemberPermissions,
        BuiltInRole.BillingManager => BillingManagerPermissions,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Every built-in role has its permissions."),
    };
}
