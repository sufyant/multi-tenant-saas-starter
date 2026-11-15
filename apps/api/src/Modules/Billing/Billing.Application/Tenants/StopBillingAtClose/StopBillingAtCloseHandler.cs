using Billing.Application.Checkouts.ExpireCheckoutSession;
using Billing.Application.Checkouts;
using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using Billing.Application.Subscriptions;
using Billing.Domain.Subscriptions;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Tenants.StopBillingAtClose;

// B8: a closed tenant pays nothing more. Its subscription ends at the end of the period it paid for, so a reopen within the period can keep
// it, and its open checkout's page is expired, so nobody pays for a closed tenant. In the tenant's transaction, which the event's envelope
// declares (W3). The provider's events then bring the new state.
public static class StopBillingAtCloseHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        TenantClosedEvent closed,
        ISubscriptionQueries subscriptions,
        ICheckoutRepository checkouts,
        CancellationToken cancellationToken)
    {
        OutgoingMessages messages = [];
        if (await subscriptions.FindSubscriptionAsync(cancellationToken) is { } subscription
            && !Subscription.HasEnded(Enum.Parse<SubscriptionStatus>(subscription.Status)) && !subscription.CancelAtPeriodEnd)
        {
            messages.Add(new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, CancelAtPeriodEnd: true));
        }

        if (await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout)
        {
            if (checkout.SessionId is { } sessionId)
            {
                messages.Add(new ExpireCheckoutSessionCommand(sessionId));
            }

            checkouts.Remove(checkout);
            await checkouts.SaveChangesAsync(cancellationToken);
        }

        return messages;
    }
}
