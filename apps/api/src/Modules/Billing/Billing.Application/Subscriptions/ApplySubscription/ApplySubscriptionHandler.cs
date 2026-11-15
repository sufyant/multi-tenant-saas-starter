using Billing.Application.Checkouts.ExpireCheckoutSession;
using Billing.Application.Checkouts;
using Billing.Contracts.Subscriptions;
using Billing.Domain.Subscriptions;
using SharedKernel;
using Wolverine;

namespace Billing.Application.Subscriptions.ApplySubscription;

// The tenant's row is locked first, so two syncs of one tenant run one after the other (K6a). A state the row has already changes nothing
// and asks nothing of ControlPlane (I1). The plan change goes through the outbox in the same transaction, and keeps the tenant (W3).
public static class ApplySubscriptionHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        ApplySubscriptionCommand command,
        ISubscriptionRepository subscriptions,
        ICheckoutRepository checkouts,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var state = new SubscriptionState(
            command.CustomerId, command.SubscriptionId, command.PlanId, Enum.Parse<BillingInterval>(command.Interval),
            Enum.Parse<SubscriptionStatus>(command.Status), command.CurrentPeriodEnd, command.CancelAtPeriodEnd);
        var now = time.GetUtcNow();
        var subscription = await subscriptions.FindForUpdateAsync(cancellationToken);
        if (subscription is null)
        {
            subscription = Subscription.Start(state, now);
            subscriptions.Add(subscription);
        }
        else if (subscription.Sync(state, now) == StateChange.Unchanged)
        {
            return [];
        }

        if (!subscription.PaysForItsPlan)
        {
            await subscriptions.SaveChangesAsync(cancellationToken);
            return [];
        }

        // B7: a subscription that pays ends the tenant's open checkout, and its page is expired, so a second member cannot pay a second one.
        OutgoingMessages messages = [];
        messages.Add(new PlanPaidEvent(Guid.CreateVersion7(now), now, command.TenantId, subscription.PlanId, command.PaidBy));
        if (await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout)
        {
            if (checkout.SessionId is { } sessionId)
            {
                messages.Add(new ExpireCheckoutSessionCommand(sessionId));
            }

            checkouts.Remove(checkout);
        }

        await subscriptions.SaveChangesAsync(cancellationToken);
        return messages;
    }
}
