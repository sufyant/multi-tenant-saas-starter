namespace ControlPlane.Contracts;

/// <summary>
/// The permission catalogue. Permissions are fixed in code, each tied to a capability. Endpoints name a permission as their
/// authorization policy (A1).
/// </summary>
public static class Permissions
{
    public const string AdminTenantsCreate = "admin.tenants.create";

    public const string TenantMembersRead = "tenant.members.read";
}
