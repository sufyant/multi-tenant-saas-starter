using Billing.Application.Customers;
using Billing.Application.Subscriptions;
using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Tenants.ResumeBillingAtReopen;

// B8: a tenant reopened before the end of its paid period keeps its subscription: the end that its closure asked for is taken back, by our
// own mark, so the provider's event of the end need not have arrived, and an end that a member asked for stays. Until the provider's state
// shows the undo, each of its events asks for it again (ApplySubscription), so a late call of the closure's end cannot win. A subscription that ended
// meanwhile stays ended; the tenant pays again by checkout. The customer is locked first (K6a, B7).
public static class ResumeBillingAtReopenHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        TenantReopenedEvent reopened,
        ICustomerStore customers,
        ISubscriptionRepository subscriptions,
        CancellationToken cancellationToken)
    {
        if (await customers.FindForUpdateAsync(cancellationToken) is not { } customer)
        {
            return [];
        }

        // A reopen older than the last closure or reopen that billing took changes nothing: events arrive in no order.
        if (!customer.Reopen(reopened.OccurredAt))
        {
            return [];
        }

        OutgoingMessages messages = [];
        if (await subscriptions.FindForUpdateAsync(cancellationToken) is { } subscription && subscription.KeepAfterReopen() is { } cancelAtPeriodEnd)
        {
            messages.Add(new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, cancelAtPeriodEnd));
        }

        await customers.SaveChangesAsync(cancellationToken);
        return messages;
    }
}
