using ControlPlane.Api.Tenant.Roles;

namespace ControlPlane.Api.Me.Tenants;

/// <summary>
/// A tenant the signed-in user belongs to: an item of their tenant list, and the tenant an invitation made them join. Its status is
/// active, or suspended or closed when its members cannot enter it. The role is the user's there.
/// </summary>
public sealed record MyTenantResponse(Guid Id, string Name, string Slug, MyTenantResponseStatus Status, RoleResponse Role);
