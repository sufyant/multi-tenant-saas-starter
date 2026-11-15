using Billing.Application.Checkouts;
using Billing.Application.Checkouts.ExpireOtherCheckouts;
using Billing.Application.Customers;
using Billing.Application.Subscriptions;
using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using Billing.Domain.Subscriptions;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Tenants.StopBillingAtClose;

// B8: a closed tenant pays nothing more. Its customer, the root of its billing, is locked first and marked closed, so a checkout of after the
// closure is refused and a subscription that arrives later ends at its period's end too. Its subscription ends at the end of the period it
// paid for, so a reopen within the period can keep it; its open checkout goes, and every open page of the customer is expired. In the
// tenant's transaction, which the event's envelope declares (W3). A tenant that never opened a checkout has no customer, and nothing to stop.
public static class StopBillingAtCloseHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        TenantClosedEvent closed,
        ICustomerStore customers,
        ISubscriptionRepository subscriptions,
        ICheckoutRepository checkouts,
        CancellationToken cancellationToken)
    {
        if (await customers.FindForUpdateAsync(cancellationToken) is not { } customer)
        {
            return [];
        }

        customer.Close(closed.OccurredAt);
        OutgoingMessages messages = [new ExpireOtherCheckoutsCommand(customer.CustomerId, KeepKey: null)];
        if (await subscriptions.FindForUpdateAsync(cancellationToken) is { } subscription
            && !Subscription.HasEnded(subscription.Status) && !subscription.CancelAtPeriodEnd)
        {
            subscription.EndByClosure();
            messages.Add(new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, CancelAtPeriodEnd: true));
        }

        if (await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout)
        {
            checkouts.Remove(checkout);
        }

        await customers.SaveChangesAsync(cancellationToken);
        return messages;
    }
}
