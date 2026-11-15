using Billing.Application.Customers;
using Billing.Application.Subscriptions;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Tenants.PurgeTenantData;

// The tenant purge asks Billing to delete the tenant's data, in the tenant's transaction (docs/flows/tenant-purge.md): its subscription's
// row and its customer in billing's map. The provider keeps its own records of the payments, which the law asks it to keep. It replies, so the purge goes on; a second copy
// deletes nothing more (S7).
public static class PurgeTenantDataHandler
{
    /// <summary>The name Billing takes part in the purge by, which its replies carry.</summary>
    public const string Participant = "Billing";

    public static async Task<OutgoingMessages> HandleAsync(
        PurgeTenantDataCommand purge,
        ISubscriptionRepository subscriptions,
        ICustomerStore customers,
        CancellationToken cancellationToken)
    {
        await subscriptions.DeleteAsync(cancellationToken);
        await customers.DeleteAsync(cancellationToken);
        return [new PurgeTenantDataReply(purge.TenantId, Participant)];
    }
}
