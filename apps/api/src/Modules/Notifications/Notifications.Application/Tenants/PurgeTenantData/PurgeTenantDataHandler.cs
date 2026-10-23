using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Notifications.Application.Tenants.PurgeTenantData;

// Notifications stores no data of a tenant: it sends an email and keeps nothing (section 6, "Tenant purge"). It still takes part in the
// tenant purge, so the purge waits for it, and the module that adds data of a tenant later deletes it here, in the tenant's transaction,
// before it replies. The handler takes no DbContext today, so Wolverine opens no transaction.
public static class PurgeTenantDataHandler
{
    /// <summary>The name Notifications takes part in the purge by, which its replies carry.</summary>
    public const string Participant = "Notifications";

    public static OutgoingMessages Handle(PurgeTenantDataCommand purge) => [new PurgeTenantDataReply(purge.TenantId, Participant)];
}
