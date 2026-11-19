using System.Security.Claims;
using System.Text.Json.Nodes;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Api.Tenant.Members;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Tenants.GetBillingEnabled;
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
    // It also says whether the deployment's billing is on (B1), so every member's web shows the billing texts (14.01, 14.03, 14.06).
    private static async Task<Result<MyTenantDetailResponse>> GetMyTenant(
        ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken)
    {
        var billingEnabled = await bus.InvokeAsync<bool>(new GetBillingEnabledQuery(), cancellationToken);
        return (await bus.InvokeAsync<Result<MyTenantDetailView>>(new GetMyTenantQuery(user.Id()), cancellationToken)).Map(tenant => ToResponse(tenant, billingEnabled));
    }

    private static MyTenantDetailResponse ToResponse(MyTenantDetailView tenant, bool billingEnabled) => new(
        tenant.Tenant.Id,
        tenant.Tenant.Name,
        tenant.Tenant.Slug,
        ResponseStatus.OfMyTenant(tenant.Tenant.Status),
        RoleEndpoints.ToResponse(tenant.Tenant.Role),
        tenant.Permissions,
        tenant.CreatedAt,
        tenant.PlanName,
        tenant.Entitlements.ToDictionary(value => value.Key, value => (JsonNode?)ValueOf(value.Value), StringComparer.Ordinal),
        tenant.Usage,
        billingEnabled);

    // A feature is true or false, a limit is its number, and an unlimited limit is null.
    private static JsonValue? ValueOf(EntitlementValueView value) =>
        value.Enabled is { } enabled ? JsonValue.Create(enabled) : value.Maximum is { } maximum ? JsonValue.Create(maximum) : null;
}
