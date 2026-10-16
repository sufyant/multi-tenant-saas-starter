namespace ControlPlane.Contracts;

/// <summary>
/// The permission catalogue. Permissions are fixed in code, each tied to a capability. Endpoints name a permission as their
/// authorization policy (A1).
/// </summary>
public static class Permissions
{
    public const string AdminTenantsCreate = "admin.tenants.create";

    public const string AdminTenantsRead = "admin.tenants.read";

    public const string AdminTenantsSuspend = "admin.tenants.suspend";

    public const string AdminTenantsReactivate = "admin.tenants.reactivate";

    public const string AdminUsersRead = "admin.users.read";

    public const string AdminUsersSuspend = "admin.users.suspend";

    public const string AdminUsersReactivate = "admin.users.reactivate";

    public const string TenantMembersRead = "tenant.members.read";

    public const string TenantMembersUpdate = "tenant.members.update";

    public const string TenantMembersDelete = "tenant.members.delete";

    public const string TenantProfileRead = "tenant.profile.read";

    public const string TenantInvitationsCreate = "tenant.invitations.create";

    public const string TenantInvitationsRead = "tenant.invitations.read";

    public const string TenantInvitationsDelete = "tenant.invitations.delete";
}
