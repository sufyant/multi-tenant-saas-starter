using System.Collections.Frozen;

namespace ControlPlane.Domain.Roles;

/// <summary>
/// The roles every tenant has. Their permissions are fixed here and they cannot be changed or deleted. They are stored once,
/// with these ids, as rows that belong to no tenant, so memberships and invitations reference every role the same way.
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

    public static Role Owner { get; } = Role.CreateBuiltIn(new("00000000-0000-7000-8000-000000000001"), BuiltInRole.Owner);

    public static Role Admin { get; } = Role.CreateBuiltIn(new("00000000-0000-7000-8000-000000000002"), BuiltInRole.Admin);

    public static Role Member { get; } = Role.CreateBuiltIn(new("00000000-0000-7000-8000-000000000003"), BuiltInRole.Member);

    public static IReadOnlyList<Role> All { get; } = [Owner, Admin, Member];

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
