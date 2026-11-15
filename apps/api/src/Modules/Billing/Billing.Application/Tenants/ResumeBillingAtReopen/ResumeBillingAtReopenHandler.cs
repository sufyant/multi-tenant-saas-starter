using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using Billing.Application.Subscriptions;
using Billing.Domain.Subscriptions;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Tenants.ResumeBillingAtReopen;

// B8: a tenant reopened before the end of its paid period keeps its subscription: the end that its closure asked for is taken back. A
// subscription that ended meanwhile stays ended; the tenant pays again by checkout.
public static class ResumeBillingAtReopenHandler
{
    public static async Task<OutgoingMessages> HandleAsync(TenantReopenedEvent reopened, ISubscriptionQueries subscriptions, CancellationToken cancellationToken) =>
        await subscriptions.FindSubscriptionAsync(cancellationToken) is { CancelAtPeriodEnd: true } subscription
            && !Subscription.HasEnded(Enum.Parse<SubscriptionStatus>(subscription.Status))
            ? [new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, CancelAtPeriodEnd: false)]
            : [];
}
