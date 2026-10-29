using ControlPlane.Contracts.Tenants;
using Notifications.Application.Notifications;
using Wolverine;

namespace Notifications.Application.Tenants.PurgeTenantData;

// The tenant purge asks Notifications to delete the tenant's data, in the tenant's transaction (docs/flows/tenant-purge.md): every
// notification about the tenant, in every recipient's list, through the narrow function that deletes those of the declared tenant (U8).
// A personal notification is about no tenant and stays. It replies, so the purge goes on; a second copy deletes nothing more (S7).
public static class PurgeTenantDataHandler
{
    /// <summary>The name Notifications takes part in the purge by, which its replies carry.</summary>
    public const string Participant = "Notifications";

    public static async Task<OutgoingMessages> HandleAsync(PurgeTenantDataCommand purge, INotificationRepository notifications, CancellationToken cancellationToken)
    {
        await notifications.PurgeTenantAsync(cancellationToken);
        return [new PurgeTenantDataReply(purge.TenantId, Participant)];
    }
}
