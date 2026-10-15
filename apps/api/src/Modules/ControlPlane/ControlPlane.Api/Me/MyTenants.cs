using System.Security.Claims;
using System.Text.Json.Serialization;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Tenants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me;

/// <summary>The tenants the signed-in user belongs to.</summary>
public static class MyTenants
{
    public static void Map(RouteGroupBuilder me) => me.MapGet("/tenants", ListMyTenants);

    // T4: the user's own active and suspended tenants, read without a tenant.
    private static async Task<Result<ListPage<MyTenantResponse>>> ListMyTenants(
        int? page,
        int? pageSize,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var paging = PageRequest.Create(page, pageSize);
        if (!paging.IsSuccess)
        {
            return paging.Error;
        }

        var tenants = await bus.InvokeAsync<Result<ListPage<TenantSummary>>>(new ListMyTenants(user.Id(), paging.Value), cancellationToken);
        return tenants.Map(list => list.Map(tenant => new MyTenantResponse(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfMyTenant(tenant.Status))));
    }
}

/// <summary>
/// A tenant the signed-in user belongs to: an item of their tenant list, and the tenant an invitation made them join. Its status is
/// active, or suspended when its members cannot enter it.
/// </summary>
public sealed record MyTenantResponse(Guid Id, string Name, string Slug, MyTenantResponseStatus Status);

/// <summary>The status of a tenant as its members see it: active, or suspended when they cannot enter it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MyTenantResponseStatus>))]
public enum MyTenantResponseStatus
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,
}
