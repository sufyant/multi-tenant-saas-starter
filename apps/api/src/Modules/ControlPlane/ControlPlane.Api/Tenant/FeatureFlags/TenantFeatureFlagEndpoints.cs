using ControlPlane.Application.FeatureFlags.GetMyTenantFeatureFlags;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant.FeatureFlags;

/// <summary>
/// The feature flags of the route's organisation, which the web's screens read (docs/flows/feature-flags.md). Every member reads them, as
/// every member reads the organisation itself, and no plan gates them (E5).
/// </summary>
public static class TenantFeatureFlagEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/feature-flags", GetMyTenantFeatureFlags).RequireAuthorization(Permissions.TenantProfileRead).WithMetadata(new NotPlanGated());

    // The tenant door has declared the tenant of the route.
    private static async Task<Result<MyFeatureFlagsResponse>> GetMyTenantFeatureFlags(IMessageBus bus, CancellationToken cancellationToken) =>
        new MyFeatureFlagsResponse(await bus.InvokeAsync<IReadOnlyDictionary<string, bool>>(new GetMyTenantFeatureFlagsQuery(), cancellationToken));
}
