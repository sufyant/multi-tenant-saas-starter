using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Me;
using ControlPlane.Application.Tenants;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant;

/// <summary>The tenant in the route itself, for its members.</summary>
public static class Profile
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet(string.Empty, GetMyTenant).RequireAuthorization(Permissions.TenantProfileRead);

    // The member's shape of a tenant (K5), with when it was created and the member's role and permissions. The tenant door has declared
    // the tenant of the route.
    private static async Task<Result<MyTenantDetailResponse>> GetMyTenant(ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MyTenantDetails>>(new GetMyTenant(user.Id()), cancellationToken))
            .Map(found => new MyTenantDetailResponse(
                found.Tenant.Id,
                found.Tenant.Name,
                found.Tenant.Slug,
                ResponseStatus.OfMyTenant(found.Tenant.Status),
                Members.ToResponse(found.Tenant.Role),
                found.Permissions,
                found.CreatedAt));
}

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
