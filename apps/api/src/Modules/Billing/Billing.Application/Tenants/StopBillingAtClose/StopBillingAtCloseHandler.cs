using Billing.Application.Checkouts;
using Billing.Application.Checkouts.ExpireCheckoutPages;
using Billing.Application.Customers;
using Billing.Application.Subscriptions;
using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Tenants.StopBillingAtClose;

// B8: a closed tenant pays nothing more. Its customer, the root of its billing, is locked first and marked closed, so a checkout of after the
// closure is refused and a subscription that arrives later ends at its period's end too. Its subscription ends at the end of the period it
// paid for, so a reopen within the period can keep it; its open checkout goes, and its pages are expired. A closure older than the last
// closure or reopen that billing took changes nothing. In the
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

        // A closure older than the last closure or reopen that billing took changes nothing: events arrive in no order.
        if (!customer.Close(closed.OccurredAt))
        {
            return [];
        }

        OutgoingMessages messages = [];
        if (await subscriptions.FindForUpdateAsync(cancellationToken) is { } subscription && subscription.EndForClosure() is { } cancelAtPeriodEnd)
        {
            messages.Add(new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, cancelAtPeriodEnd));
        }

        if (await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout)
        {
            checkouts.Remove(checkout);
            messages.Add(new ExpireCheckoutPagesCommand(customer.CustomerId, checkout.Key));
        }

        await customers.SaveChangesAsync(cancellationToken);
        return messages;
    }
}
