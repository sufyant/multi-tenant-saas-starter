using ControlPlane.Domain.Roles;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What a change to a custom role needs to know of the tenant's rows (<see cref="Tenant.CreateRole"/>, <see cref="Tenant.UpdateRole"/>,
/// <see cref="Tenant.DeleteRole"/>), read under the tenant's lock (K6a): the role it changes, with its permissions, none when the tenant has
/// no such role; how many custom roles the tenant has; whether another role of the tenant has the name, compared without regard to case,
/// as the unique index compares it; and what uses the role, nothing when there is no role.
/// </summary>
internal sealed record RoleFacts(Role? Target, int CustomRoles, bool NameTaken, RoleUse Use);
