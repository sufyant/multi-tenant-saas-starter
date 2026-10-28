using System.Security.Claims;
using System.Text.Json.Nodes;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Api.Tenant.Members;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Tenants.GetMyTenant;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;
using ControlPlane.Api.Tenant.Roles;

namespace ControlPlane.Api.Tenant.Tenants;

/// <summary>The tenant in the route itself, for its members.</summary>
public static class TenantProfileEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet(string.Empty, GetMyTenant).RequireAuthorization(Permissions.TenantProfileRead).WithMetadata(new NotPlanGated());

    // The member's shape of a tenant (K5), with when it was created and the member's role and permissions. The tenant door has declared
    // the tenant of the route.
    private static async Task<Result<MyTenantDetailResponse>> GetMyTenant(ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MyTenantDetailView>>(new GetMyTenantQuery(user.Id()), cancellationToken)).Map(ToResponse);

    private static MyTenantDetailResponse ToResponse(MyTenantDetailView tenant) => new(
        tenant.Tenant.Id,
        tenant.Tenant.Name,
        tenant.Tenant.Slug,
        ResponseStatus.OfMyTenant(tenant.Tenant.Status),
        RoleEndpoints.ToResponse(tenant.Tenant.Role),
        tenant.Permissions,
        tenant.CreatedAt,
        tenant.PlanName,
        tenant.Entitlements.ToDictionary(value => value.Key, value => (JsonNode?)ValueOf(value.Value), StringComparer.Ordinal),
        tenant.Usage);

    // A feature is true or false, a limit is its number, and an unlimited limit is null.
    private static JsonValue? ValueOf(EntitlementValueView value) =>
        value.Enabled is { } enabled ? JsonValue.Create(enabled) : value.Maximum is { } maximum ? JsonValue.Create(maximum) : null;
}
