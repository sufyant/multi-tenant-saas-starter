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

    // The member's shape of a tenant (K5): an item of their tenant list. The tenant door has declared the tenant of the route.
    private static async Task<Result<MyTenantResponse>> GetMyTenant(IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<TenantSummary>>(new GetMyTenant(), cancellationToken))
            .Map(tenant => new MyTenantResponse(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfMyTenant(tenant.Status)));
}
