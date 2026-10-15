using System.Collections.Frozen;

namespace ControlPlane.Domain;

/// <summary>
/// The permissions of the catalogue in their two pools. Tenant roles choose from the tenant pool only; system admins hold the
/// system pool (A4). Domain does not reference Contracts (section 2), so the names are stated here as well; a unit test keeps
/// them the same as the catalogue in <c>ControlPlane.Contracts</c>.
/// </summary>
internal static class PermissionPools
{
    public const string AdminTenantsCreate = "admin.tenants.create";

    public const string AdminTenantsRead = "admin.tenants.read";

    public const string AdminTenantsSuspend = "admin.tenants.suspend";

    public const string AdminTenantsReactivate = "admin.tenants.reactivate";

    public const string AdminUsersRead = "admin.users.read";

    public const string AdminUsersSuspend = "admin.users.suspend";

    public const string AdminUsersReactivate = "admin.users.reactivate";

    public const string TenantMembersRead = "tenant.members.read";

    public const string TenantProfileRead = "tenant.profile.read";

    public static IReadOnlySet<string> TenantPool { get; } = FrozenSet.Create(StringComparer.Ordinal, TenantMembersRead, TenantProfileRead);

    public static IReadOnlySet<string> SystemPool { get; } = FrozenSet.Create(
        StringComparer.Ordinal,
        AdminTenantsCreate,
        AdminTenantsRead,
        AdminTenantsSuspend,
        AdminTenantsReactivate,
        AdminUsersRead,
        AdminUsersSuspend,
        AdminUsersReactivate);
}
