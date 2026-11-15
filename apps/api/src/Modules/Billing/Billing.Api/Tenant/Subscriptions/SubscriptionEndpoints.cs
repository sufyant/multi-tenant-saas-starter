using Billing.Api.Statuses;
using Billing.Application.Subscriptions.GetBilling;
using Billing.Application.Subscriptions;
using Billing.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>The billing of the tenant in the route, for a member who manages it.</summary>
public static class SubscriptionEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/billing", GetBilling).RequireAuthorization(BillingPermissions.TenantBillingManage).WithMetadata(new NotPlanGated());

    // The tenant comes from the route: the host has declared it, and row level security keeps the read to it.
    private static async Task<Result<BillingResponse>> GetBilling(BillingOptions billing, IMessageBus bus, CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        var subscription = await bus.InvokeAsync<SubscriptionView?>(new GetBillingQuery(), cancellationToken);
        return new BillingResponse(subscription is null
            ? null
            : new SubscriptionResponse(
                subscription.PlanId,
                ResponseStatus.OfInterval(subscription.Interval),
                ResponseStatus.OfSubscription(subscription.Status),
                subscription.CurrentPeriodEnd,
                subscription.CancelAtPeriodEnd,
                subscription is { ScheduledPlanId: { } planId, ScheduledInterval: { } interval }
                    ? new ScheduledChangeResponse(planId, ResponseStatus.OfInterval(interval))
                    : null));
    }
}
