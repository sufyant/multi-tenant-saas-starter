using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Usage;
using ControlPlane.Application;
using ControlPlane.Application.Tenants.GetPaysByCard;
using ControlPlane.Application.Usage.GetUsage;
using ControlPlane.Application.Usage.SetSpendingCap;
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
    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/usage", GetUsage).RequireAuthorization(Permissions.TenantBillingManage).WithMetadata(new NotPlanGated());
        tenant.MapPut("/usage/spending-cap", SetSpendingCap).RequireAuthorization(Permissions.TenantBillingManage).WithMetadata(new NotPlanGated());
    }

    // Billing answers first whether the tenant pays by card, in a query of its own, as a handler gets the transaction of one module's
    // DbContext (W1): only such a tenant is offered a cap (E9).
    // The platform's Usage tab answers the same (TenantUsageEndpoints).
    internal static async Task<Result<UsageResponse>> ReadAsync(Guid tenantId, IMessageBus bus, BillingOptions billing, CancellationToken cancellationToken)
    {
        var paysByCard = await bus.InvokeAsync<bool>(new GetPaysByCardQuery(tenantId), cancellationToken);
        return (await bus.InvokeForTenantAsync<Result<UsageView>>(tenantId.ToString(), new GetUsageQuery(paysByCard), cancellationToken))
            .Map(usage => ToResponse(usage, billing));
    }

    private static UsageResponse ToResponse(UsageView usage, BillingOptions billing) => new(
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
            [.. period.Meters.Select(meter => new PastMeterUsageResponse(meter.Key, meter.Used, meter.Extra, meter.ExtraAmount))]))],
        usage.SpendingCap is { } cap ? new SpendingCapResponse(cap.Amount, cap.ExtraAmount, cap.ReachedAt) : null,
        usage.SpendingCapOffered);

    private static Task<Result<UsageResponse>> GetUsage(Guid tenantId, IMessageBus bus, BillingOptions billing, CancellationToken cancellationToken) =>
        ReadAsync(tenantId, bus, billing, cancellationToken);

    // Only a tenant that pays by card has a cap (E9); Billing answers it before the command locks the tenant. The cap it has already
    // changes nothing, so the request takes no idempotency key (I1).
    private static async Task<Result> SetSpendingCap(
        Guid tenantId,
        SetSpendingCapRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (!await bus.InvokeAsync<bool>(new GetPaysByCardQuery(tenantId), cancellationToken))
        {
            return Errors.SpendingCapUnavailable;
        }

        return await bus.InvokeAsync<Result>(new SetSpendingCapCommand(user.Id(), request.Amount), cancellationToken);
    }
}
