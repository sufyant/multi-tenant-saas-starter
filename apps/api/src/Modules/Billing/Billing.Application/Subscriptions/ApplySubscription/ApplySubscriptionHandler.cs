using Billing.Domain.Subscriptions;
using Billing.Contracts.Subscriptions;
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

        await subscriptions.SaveChangesAsync(cancellationToken);
        return subscription.PaysForItsPlan ? [new PlanPaidEvent(Guid.CreateVersion7(now), now, command.TenantId, subscription.PlanId, command.PaidBy)] : [];
    }
}
