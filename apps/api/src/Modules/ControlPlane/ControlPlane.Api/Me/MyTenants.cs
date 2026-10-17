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

        var tenants = await bus.InvokeAsync<Result<ListPage<TenantSummary>>>(new ListMyTenants(user.Id(), list.Value), cancellationToken);
        return tenants.Map(found => found.Map(ToResponse));
    }

    // Accepting an invitation answers the tenant it joined in the same shape (N7: a response that two audiences answer stays in the
    // first one's file).
    internal static MyTenantResponse ToResponse(TenantSummary tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfMyTenant(tenant.Status), tenant.Role);
}

/// <summary>
/// A tenant the signed-in user belongs to: an item of their tenant list, and the tenant an invitation made them join. Its status is
/// active, or suspended or closed when its members cannot enter it. The role is the user's there: <c>owner</c>, <c>admin</c> or
/// <c>member</c>.
/// </summary>
public sealed record MyTenantResponse(Guid Id, string Name, string Slug, MyTenantResponseStatus Status, string Role);

/// <summary>The status of a tenant as its members see it: active, or suspended or closed when they cannot enter it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MyTenantResponseStatus>))]
public enum MyTenantResponseStatus
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,

    [JsonStringEnumMemberName("closed")]
    Closed,
}
