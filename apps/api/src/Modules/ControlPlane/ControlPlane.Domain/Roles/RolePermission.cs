using SharedKernel;

namespace ControlPlane.Domain.Roles;

/// <summary>
/// One permission of a custom role, a row of <c>role_permissions</c> (A3; NIST RBAC, permission assignment). A built-in role has none:
/// its permissions come from code (<see cref="BuiltInRoles"/>).
/// </summary>
internal sealed class RolePermission(Guid tenantId, Guid roleId, string permission) : ITenantEntity
{
    public Guid TenantId { get; private init; } = tenantId;

    public Guid RoleId { get; private init; } = roleId;

    public string Permission { get; private init; } = permission;
}
