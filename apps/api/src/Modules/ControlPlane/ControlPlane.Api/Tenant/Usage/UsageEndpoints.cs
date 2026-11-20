using ControlPlane.Api.Statuses;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Usage;
using ControlPlane.Application.Usage.GetUsage;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant.Usage;

/// <summary>
/// The use of the tenant's meters (step 40c): Usage for a member who manages billing (11.01, 14.12). A plan without a meter answers none,
/// so the route needs no feature of the plan.
/// </summary>
public static class UsageEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/usage", GetUsage).RequireAuthorization(Permissions.TenantBillingManage).WithMetadata(new NotPlanGated());

    // The platform's Usage tab answers the same (TenantUsageEndpoints).
    internal static UsageResponse ToResponse(UsageView usage, BillingOptions billing) => new(
        usage.PeriodStart,
        usage.PeriodEnd,
        usage.PlanName,
        billing.Currency,
        [.. usage.Meters.Select(meter => new MeterUsageResponse(
            meter.Key,
            meter.Quota,
            ResponseStatus.OfMeterOverage(meter.Overage),
            meter.UnitPrice,
            meter.UnitSize,
            meter.Used,
            meter.Extra,
            meter.ExtraAmount,
            meter.QuotaReachedAt))],
        [.. usage.PastPeriods.Select(period => new PastUsageResponse(
            period.PeriodStart,
            [.. period.Meters.Select(meter => new PastMeterUsageResponse(meter.Key, meter.Used, meter.Extra, meter.ExtraAmount))]))]);

    private static async Task<Result<UsageResponse>> GetUsage(IMessageBus bus, BillingOptions billing, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UsageView>>(new GetUsageQuery(), cancellationToken)).Map(usage => ToResponse(usage, billing));
}
