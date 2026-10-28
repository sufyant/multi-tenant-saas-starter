using System.Text.Json.Nodes;
using ControlPlane.Api.Me.Tenants;
using ControlPlane.Api.Tenant.Roles;

namespace ControlPlane.Api.Tenant.Tenants;

/// <summary>
/// The tenant of the route, for one of its members: as an item of their tenant list, with when it was created and the names of the
/// permissions the member's role holds there, in ordinal order. The web shows an action only when the list holds its permission; every
/// endpoint still checks the permission itself. It also holds the name of the tenant's plan; <c>entitlements</c>, every key of the list
/// with its effective value at the moment of the request, by key, as the permissions: a feature true or false, a limit a number, and null
/// for an unlimited limit; and <c>usage</c>, the tenant's count of each limit, so the web adds no list up. Every endpoint still checks
/// its feature and its limit itself.
/// </summary>
public sealed record MyTenantDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    MyTenantResponseStatus Status,
    RoleResponse Role,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedAt,
    string PlanName,
    IReadOnlyDictionary<string, JsonNode?> Entitlements,
    IReadOnlyDictionary<string, int> Usage);
