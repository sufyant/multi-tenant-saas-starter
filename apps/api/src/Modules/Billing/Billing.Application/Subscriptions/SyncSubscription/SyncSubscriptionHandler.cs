using Billing.Application.Provider;
using Billing.Application.Subscriptions.RouteSubscription;
using Billing.Domain.Subscriptions;
using Wolverine;

namespace Billing.Application.Subscriptions.SyncSubscription;

// The handler takes no DbContext, so no transaction is open during the provider's call (section 3, "Handlers and pipeline"). A call that
// fails is tried again (BillingProviderRetries). A subscription that pays for no plan of ours is not ours, so nothing follows; the tenant is
// found in our own map, by the next message (B5).
public static class SyncSubscriptionHandler
{
    public static async Task<OutgoingMessages> HandleAsync(SyncSubscriptionCommand command, IBillingProvider provider, CancellationToken cancellationToken)
    {
        if (await provider.FindSubscriptionAsync(command.SubscriptionId, cancellationToken) is not { } found)
        {
            return [];
        }

        return
        [
            new RouteSubscriptionCommand(
                found.CustomerId, found.SubscriptionId, found.PlanId, IntervalOf(found.Yearly),
                found.Status, found.CurrentPeriodEnd, found.CancelAtPeriodEnd, found.ScheduledPlanId, IntervalOf(found.ScheduledYearly), found.People, found.Payment,
                found.BaseAmount, found.PerPersonAmount, found.LastPaidAt),
        ];
    }

    private static string IntervalOf(bool yearly) => yearly ? nameof(BillingInterval.Yearly) : nameof(BillingInterval.Monthly);

    private static string? IntervalOf(bool? yearly) => yearly is { } known ? IntervalOf(known) : null;
}
