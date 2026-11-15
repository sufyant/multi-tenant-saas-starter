using Billing.Application.Customers;
using Billing.Application.Subscriptions.GetBilling;
using Billing.Domain.Subscriptions;
using SharedKernel;

namespace Billing.Application.Subscriptions.BeginPlanChange;

// The tenant's customer, the root of its billing, is locked first, then its subscription (K6a, B7), so a change waits for a closure that
// holds the lock, and refuses the closed tenant. The provider's call follows after the commit, with no transaction open, so a closure that
// arrives in between finds the subscription as the member left it and ends it at the period's end (B8). Every rejection comes before any
// change (W7).
public static class BeginPlanChangeHandler
{
    public static async Task<Result<SubscriptionView>> HandleAsync(
        BeginPlanChangeCommand command,
        ICustomerStore customers,
        ISubscriptionRepository subscriptions,
        CancellationToken cancellationToken)
    {
        if (await customers.FindForUpdateAsync(cancellationToken) is not { } customer)
        {
            return Errors.NoSubscription;
        }

        if (customer.ClosedAt is not null)
        {
            return Errors.TenantClosed;
        }

        if (await subscriptions.FindForUpdateAsync(cancellationToken) is not { } subscription || Subscription.HasEnded(subscription.Status))
        {
            return Errors.NoSubscription;
        }

        if (!subscription.PaysForItsPlan)
        {
            return Errors.SubscriptionNotActive;
        }

        if (command.Undo && !subscription.CancelAtPeriodEnd && subscription.ScheduledPlanId is null)
        {
            return Errors.NothingScheduled;
        }

        customer.NameMember(command.ActorId);
        if (command.ByMemberEnd)
        {
            subscription.EndByMember();
        }

        await subscriptions.SaveChangesAsync(cancellationToken);
        return new SubscriptionView(
            subscription.CustomerId, subscription.SubscriptionId, subscription.PlanId, subscription.Interval.ToString(), subscription.Status.ToString(),
            subscription.CurrentPeriodEnd, subscription.CancelAtPeriodEnd, subscription.ScheduledPlanId, subscription.ScheduledInterval?.ToString());
    }
}
