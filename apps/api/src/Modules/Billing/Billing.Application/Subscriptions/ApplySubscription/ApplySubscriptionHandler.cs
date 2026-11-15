using Billing.Application.Checkouts;
using Billing.Application.Checkouts.ExpireOtherCheckouts;
using Billing.Application.Customers;
using Billing.Application.Subscriptions.CancelSubscription;
using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using Billing.Contracts.Subscriptions;
using Billing.Domain.Subscriptions;
using SharedKernel;
using Wolverine;

namespace Billing.Application.Subscriptions.ApplySubscription;

// The tenant's customer, the root of its billing, is locked first, then its subscription (K6a, B7), so two syncs of one tenant run one after
// the other. A state the row has already changes nothing and asks nothing of ControlPlane (I1). Each message goes through the outbox in the
// same transaction, and keeps the tenant (W3).
public static class ApplySubscriptionHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        ApplySubscriptionCommand command,
        ICustomerStore customers,
        ISubscriptionRepository subscriptions,
        ICheckoutRepository checkouts,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var state = new SubscriptionState(
            command.CustomerId, command.SubscriptionId, command.PlanId, Enum.Parse<BillingInterval>(command.Interval),
            Enum.Parse<SubscriptionStatus>(command.Status), command.CurrentPeriodEnd, command.CancelAtPeriodEnd);
        var now = time.GetUtcNow();
        var customer = await customers.FindForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A subscription is routed to the tenant of its customer.");
        var subscription = await subscriptions.FindForUpdateAsync(cancellationToken);

        // B7: a second subscription while one has not ended came from a page that a race left payable. It ends at once, and support refunds
        // its payment; the tenant keeps the first.
        if (subscription is not null && subscription.SubscriptionId != state.SubscriptionId
            && !Subscription.HasEnded(subscription.Status) && !Subscription.HasEnded(state.Status))
        {
            return [new CancelSubscriptionCommand(state.SubscriptionId)];
        }

        OutgoingMessages messages = [];
        if (subscription is null)
        {
            subscription = Subscription.Start(state, now);
            subscriptions.Add(subscription);
        }
        else if (subscription.Sync(state, now) == StateChange.Unchanged)
        {
            return [];
        }

        // B8: a subscription of a closed tenant pays nothing after its period, however late its state arrives.
        if (customer.ClosedAt is not null && !Subscription.HasEnded(subscription.Status) && !subscription.CancelAtPeriodEnd)
        {
            subscription.EndByClosure();
            messages.Add(new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, CancelAtPeriodEnd: true));
        }

        // B7: a subscription that pays ends the tenant's open checkout, and every open page of the customer is expired, so nobody pays twice.
        if (subscription.PaysForItsPlan)
        {
            messages.Add(new PlanPaidEvent(Guid.CreateVersion7(now), now, command.TenantId, subscription.PlanId, command.PaidBy));
            if (await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout)
            {
                checkouts.Remove(checkout);
            }

            messages.Add(new ExpireOtherCheckoutsCommand(customer.CustomerId, KeepKey: null));
        }

        await subscriptions.SaveChangesAsync(cancellationToken);
        return messages;
    }
}
