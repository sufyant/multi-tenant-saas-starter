using Billing.Application.Checkouts;
using Billing.Application.Checkouts.ExpireCheckoutPages;
using Billing.Application.Customers;
using Billing.Application.Subscriptions;
using Billing.Application.Subscriptions.CancelSubscription;
using Billing.Domain.Subscriptions;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Tenants.PurgeTenantData;

// The tenant purge asks Billing to delete the tenant's data, in the tenant's transaction (docs/flows/tenant-purge.md): its subscription's
// row, its open checkout and its customer in billing's map. The customer, the root of its billing, is locked first (K6a, B7). A
// subscription that has not ended ends at the provider at once, and the open checkout's pages are expired, through the outbox in the
// same transaction (B8). The provider keeps its own records of the payments, which the law asks it to keep. It replies, so the purge goes
// on; a second copy deletes nothing more (S7).
public static class PurgeTenantDataHandler
{
    /// <summary>The name Billing takes part in the purge by, which its replies carry.</summary>
    public const string Participant = "Billing";

    public static async Task<OutgoingMessages> HandleAsync(
        PurgeTenantDataCommand purge,
        ICustomerStore customers,
        ISubscriptionRepository subscriptions,
        ICheckoutRepository checkouts,
        CancellationToken cancellationToken)
    {
        OutgoingMessages messages = [];
        if (await customers.FindForUpdateAsync(cancellationToken) is { } customer)
        {
            if (await subscriptions.FindForUpdateAsync(cancellationToken) is { } subscription && !Subscription.HasEnded(subscription.Status))
            {
                messages.Add(new CancelSubscriptionCommand(subscription.SubscriptionId));
            }

            if (await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout)
            {
                messages.Add(new ExpireCheckoutPagesCommand(customer.CustomerId, checkout.Key));
            }
        }

        await checkouts.DeleteAsync(cancellationToken);
        await subscriptions.DeleteAsync(cancellationToken);
        await customers.DeleteAsync(cancellationToken);
        messages.Add(new PurgeTenantDataReply(purge.TenantId, Participant));
        return messages;
    }
}
