using System.Collections.Frozen;

namespace ControlPlane.Domain;

/// <summary>
/// The permissions of the catalogue in their two pools. Tenant roles choose from the tenant pool only; system admins hold the
/// system pool (A4). Domain does not reference Contracts (section 2), so the names are stated here as well; a unit test keeps
/// them the same as the catalogue in <c>ControlPlane.Contracts</c>.
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

    public const string PlatformAuditRead = "platform.audit.read";

    public const string TenantMembersRead = "tenant.members.read";

    public const string TenantMembersUpdate = "tenant.members.update";

    public const string TenantMembersDelete = "tenant.members.delete";

    public const string TenantProfileRead = "tenant.profile.read";

    public const string TenantInvitationsCreate = "tenant.invitations.create";

    public const string TenantInvitationsRead = "tenant.invitations.read";

    public const string TenantInvitationsDelete = "tenant.invitations.delete";

    public const string TenantAuditRead = "tenant.audit.read";

    public static IReadOnlySet<string> TenantPool { get; } = FrozenSet.Create(
        StringComparer.Ordinal,
        TenantMembersRead,
        TenantMembersUpdate,
        TenantMembersDelete,
        TenantProfileRead,
        TenantInvitationsCreate,
        TenantInvitationsRead,
        TenantInvitationsDelete,
        TenantAuditRead);

    public static IReadOnlySet<string> SystemPool { get; } = FrozenSet.Create(
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
        PlatformAuditRead);
}
