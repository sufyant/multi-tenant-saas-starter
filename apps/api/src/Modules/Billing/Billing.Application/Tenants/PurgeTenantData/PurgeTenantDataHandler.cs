using Billing.Application.Checkouts.ExpireCheckoutSession;
using Billing.Application.Checkouts;
using Billing.Application.Customers;
using Billing.Application.Subscriptions.CancelSubscription;
using Billing.Application.Subscriptions;
using Billing.Domain.Subscriptions;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Tenants.PurgeTenantData;

// The tenant purge asks Billing to delete the tenant's data, in the tenant's transaction (docs/flows/tenant-purge.md): its subscription's
// row, its open checkout and its customer in billing's map. A subscription that has not ended ends at the provider at once, and an open
// checkout's page is expired, through the outbox in the same transaction (B8). The provider keeps its own records of the payments, which the law asks it to keep. It replies, so the purge goes on; a second copy
// deletes nothing more (S7).
public static class PurgeTenantDataHandler
{
    /// <summary>The name Billing takes part in the purge by, which its replies carry.</summary>
    public const string Participant = "Billing";

    public static async Task<OutgoingMessages> HandleAsync(
        PurgeTenantDataCommand purge,
        ISubscriptionRepository subscriptions,
        ISubscriptionQueries subscriptionQueries,
        ICheckoutRepository checkouts,
        ICustomerStore customers,
        CancellationToken cancellationToken)
    {
        OutgoingMessages messages = [];
        if (await subscriptionQueries.FindSubscriptionAsync(cancellationToken) is { } subscription
            && !Subscription.HasEnded(Enum.Parse<SubscriptionStatus>(subscription.Status)))
        {
            messages.Add(new CancelSubscriptionCommand(subscription.SubscriptionId));
        }

        if (await checkouts.FindForUpdateAsync(cancellationToken) is { SessionId: { } sessionId })
        {
            messages.Add(new ExpireCheckoutSessionCommand(sessionId));
        }

        await checkouts.DeleteAsync(cancellationToken);
        await subscriptions.DeleteAsync(cancellationToken);
        await customers.DeleteAsync(cancellationToken);
        messages.Add(new PurgeTenantDataReply(purge.TenantId, Participant));
        return messages;
    }
}
