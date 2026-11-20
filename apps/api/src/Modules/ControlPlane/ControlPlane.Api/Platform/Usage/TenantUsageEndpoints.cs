using ControlPlane.Api.Tenant.Usage;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Usage;
using ControlPlane.Application.Usage.GetUsage;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform.Usage;

/// <summary>The platform's Usage tab of an organisation (step 40c; 4.37, 4.42): the answer of the tenant's own Usage.</summary>
public static class TenantUsageEndpoints
{
    public static void Map(RouteGroupBuilder platform) =>
        platform.MapGet("/tenants/{tenantId:guid}/usage", GetTenantUsage).RequireAuthorization(Permissions.PlatformTenantsRead);

    private static async Task<Result<UsageResponse>> GetTenantUsage(Guid tenantId, IMessageBus bus, BillingOptions billing, CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<UsageView>>(tenantId.ToString(), new GetUsageQuery(), cancellationToken))
            .Map(usage => UsageEndpoints.ToResponse(usage, billing));
}
