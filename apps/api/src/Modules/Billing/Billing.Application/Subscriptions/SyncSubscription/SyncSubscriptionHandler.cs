using Billing.Application.Provider;
using Billing.Application.Subscriptions.ApplySubscription;
using Billing.Domain.Subscriptions;
using Wolverine;

namespace Billing.Application.Subscriptions.SyncSubscription;

// The handler takes no DbContext, so no transaction is open during the provider's call (section 3, "Handlers and pipeline"). A call that
// fails is tried again (BillingProviderRetries). A subscription that names no tenant is not one of ours, so nothing follows.
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
            new ApplySubscriptionCommand(
                    found.TenantId, found.PaidBy, found.CustomerId, found.SubscriptionId, found.PlanId,
                    found.Yearly ? nameof(BillingInterval.Yearly) : nameof(BillingInterval.Monthly), found.Status, found.CurrentPeriodEnd, found.CancelAtPeriodEnd)
                .WithDeliveryOptions(new DeliveryOptions { TenantId = found.TenantId.ToString() }),
        ];
    }
}
