using ControlPlane.Api.Me.Tenants;
using ControlPlane.Api.Tenant.Roles;

namespace ControlPlane.Api.Tenant.Tenants;

/// <summary>
/// The tenant of the route, for one of its members: as an item of their tenant list, with when it was created and the names of the
/// permissions the member's role holds there, in ordinal order. The web shows an action only when the list holds its permission; every
/// endpoint still checks the permission itself.
/// </summary>
public sealed record MyTenantDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    MyTenantResponseStatus Status,
    RoleResponse Role,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedAt);
