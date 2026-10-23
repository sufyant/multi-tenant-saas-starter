using System.Collections.Frozen;

namespace ControlPlane.Domain;

/// <summary>
/// The permissions of the permission list in their two pools. Tenant roles choose from the tenant pool only; system admins hold the
/// platform pool (A4). The names are declared once, here. Contracts does not reference Domain (section 2), so
/// <c>apps/api/scripts/generate-permissions.mjs</c> writes the permission list, <c>ControlPlane.Contracts/Permissions.g.cs</c>, from these
/// constants (D1).
/// </summary>
internal static class PermissionPools
{
    public const string PlatformTenantsCreate = "platform.tenants.create";

    public const string PlatformTenantsRead = "platform.tenants.read";

    public const string PlatformTenantsSuspend = "platform.tenants.suspend";

    public const string PlatformTenantsReactivate = "platform.tenants.reactivate";

    public const string PlatformTenantsUpdate = "platform.tenants.update";

    public const string PlatformTenantsClose = "platform.tenants.close";

    public const string PlatformTenantsReopen = "platform.tenants.reopen";

    public const string PlatformTenantsInviteOwner = "platform.tenants.invite_owner";

    public const string PlatformUsersRead = "platform.users.read";

    public const string PlatformUsersSuspend = "platform.users.suspend";

    public const string PlatformUsersReactivate = "platform.users.reactivate";

    public const string PlatformUsersUpdate = "platform.users.update";

    public const string PlatformUsersDelete = "platform.users.delete";

    public const string PlatformSystemAdminsCreate = "platform.system_admins.create";

    public const string PlatformSystemAdminsDelete = "platform.system_admins.delete";

    public const string PlatformAuditRead = "platform.audit.read";

    public const string TenantMembersRead = "tenant.members.read";

    public const string TenantMembersUpdate = "tenant.members.update";

    public const string TenantMembersDelete = "tenant.members.delete";

    public const string TenantOwnersManage = "tenant.owners.manage";

    public const string TenantProfileRead = "tenant.profile.read";

    public const string TenantInvitationsCreate = "tenant.invitations.create";

    public const string TenantInvitationsRead = "tenant.invitations.read";

    public const string TenantInvitationsDelete = "tenant.invitations.delete";

    public const string TenantAuditRead = "tenant.audit.read";

    public const string TenantRolesRead = "tenant.roles.read";

    public const string TenantRolesCreate = "tenant.roles.create";

    public const string TenantRolesUpdate = "tenant.roles.update";

    public const string TenantRolesDelete = "tenant.roles.delete";

    public static IReadOnlySet<string> TenantPool { get; } = FrozenSet.Create(
        StringComparer.Ordinal,
        TenantMembersRead,
        TenantMembersUpdate,
        TenantMembersDelete,
        TenantOwnersManage,
        TenantProfileRead,
        TenantInvitationsCreate,
        TenantInvitationsRead,
        TenantInvitationsDelete,
        TenantAuditRead,
        TenantRolesRead,
        TenantRolesCreate,
        TenantRolesUpdate,
        TenantRolesDelete);

    /// <summary>
    /// The permissions of the tenant pool that only the Owner role holds (K1): giving the Owner role, changing or removing an Owner, and
    /// inviting an Owner (K1). A custom role never holds one (A3).
    /// </summary>
    public static IReadOnlySet<string> OwnerOnly { get; } = FrozenSet.Create(StringComparer.Ordinal, TenantOwnersManage);

    public static IReadOnlySet<string> PlatformPool { get; } = FrozenSet.Create(
        StringComparer.Ordinal,
        PlatformTenantsCreate,
        PlatformTenantsRead,
        PlatformTenantsSuspend,
        PlatformTenantsReactivate,
        PlatformTenantsUpdate,
        PlatformTenantsClose,
        PlatformTenantsReopen,
        PlatformTenantsInviteOwner,
        PlatformUsersRead,
        PlatformUsersSuspend,
        PlatformUsersReactivate,
        PlatformUsersUpdate,
        PlatformUsersDelete,
        PlatformSystemAdminsCreate,
        PlatformSystemAdminsDelete,
        PlatformAuditRead);
}
