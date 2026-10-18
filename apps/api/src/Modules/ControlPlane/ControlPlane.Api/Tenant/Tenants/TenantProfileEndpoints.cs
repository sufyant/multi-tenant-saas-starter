using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Api.Tenant.Members;
using ControlPlane.Application.Tenants.GetMyTenant;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant.Tenants;

/// <summary>The tenant in the route itself, for its members.</summary>
public static class TenantProfileEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet(string.Empty, GetMyTenant).RequireAuthorization(Permissions.TenantProfileRead);

    // The member's shape of a tenant (K5), with when it was created and the member's role and permissions. The tenant door has declared
    // the tenant of the route.
    private static async Task<Result<MyTenantDetailResponse>> GetMyTenant(ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MyTenantDetailView>>(new GetMyTenantQuery(user.Id()), cancellationToken))
            .Map(found => new MyTenantDetailResponse(
                found.Tenant.Id,
                found.Tenant.Name,
                found.Tenant.Slug,
                ResponseStatus.OfMyTenant(found.Tenant.Status),
                MemberEndpoints.ToResponse(found.Tenant.Role),
                found.Permissions,
                found.CreatedAt));
}
