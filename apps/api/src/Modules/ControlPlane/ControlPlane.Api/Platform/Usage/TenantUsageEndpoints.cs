using ControlPlane.Api.Tenant.Usage;
using ControlPlane.Application.Plans;
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

    private static Task<Result<UsageResponse>> GetTenantUsage(Guid tenantId, IMessageBus bus, BillingOptions billing, CancellationToken cancellationToken) =>
        UsageEndpoints.ReadAsync(tenantId, bus, billing, cancellationToken);
}
