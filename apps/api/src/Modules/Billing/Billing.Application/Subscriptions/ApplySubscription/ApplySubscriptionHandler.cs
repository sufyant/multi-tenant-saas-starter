using Billing.Application.Checkouts;
using Billing.Application.Checkouts.ExpireCheckoutPages;
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
            Enum.Parse<SubscriptionStatus>(command.Status), command.CurrentPeriodEnd, command.CancelAtPeriodEnd,
            command.ScheduledPlanId, command.ScheduledInterval is { } scheduled ? Enum.Parse<BillingInterval>(scheduled) : null);
        var now = time.GetUtcNow();
        // A purge that deleted the customer since the route leaves nothing to apply (B5).
        if (await customers.FindForUpdateAsync(cancellationToken) is not { } customer)
        {
            return [];
        }

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

        // B8: the provider ends in what the closure or the reopen asked, whatever order our calls reached it in, and a subscription of a
        // closed tenant pays nothing after its period, however late its state arrives.
        if (subscription.ClosureCorrection(customer.ClosedAt is not null) is { } cancelAtPeriodEnd)
        {
            messages.Add(new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, cancelAtPeriodEnd));
        }

        // B7: a subscription that pays ends the tenant's open checkout, whose pages are expired, so nobody pays twice.
        if (subscription.PaysForItsPlan)
        {
            messages.Add(new PlanPaidEvent(Guid.CreateVersion7(now), now, command.TenantId, subscription.PlanId, command.PaidBy));
            if (await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout)
            {
                checkouts.Remove(checkout);
                messages.Add(new ExpireCheckoutPagesCommand(customer.CustomerId, checkout.Key));
            }
        }

        await subscriptions.SaveChangesAsync(cancellationToken);
        return messages;
    }
}
