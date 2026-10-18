using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Api.Tenant.Members;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.ListMyTenants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me.Tenants;

/// <summary>The tenants the signed-in user belongs to.</summary>
public static class MyTenantEndpoints
{
    public static void Map(RouteGroupBuilder me) => me.MapGet("/tenants", ListMyTenants);

    // T4: the user's own active and suspended tenants, read without a tenant.
    private static async Task<Result<ListPage<MyTenantResponse>>> ListMyTenants(
        int? page,
        int? pageSize,
        ListSort<ListMyTenantsSort>? sort,
        string? q,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var tenants = await bus.InvokeAsync<Result<ListPage<MyTenantView>>>(new ListMyTenantsQuery(user.Id(), list.Value), cancellationToken);
        return tenants.Map(found => found.Map(ToResponse));
    }

    // Accepting an invitation answers the tenant it joined in the same shape (N7: a response that two audiences answer stays in the
    // first one's file).
    internal static MyTenantResponse ToResponse(MyTenantView tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfMyTenant(tenant.Status), MemberEndpoints.ToResponse(tenant.Role));
}
