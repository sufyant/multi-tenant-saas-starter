using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantPurged;

// "An organisation's purge finished", to every system admin, in the platform's bell. The platform acted, so nobody is left out. The purge
// deleted the notifications about the tenant before this one is written, so this one stays.
public static class NotifyTenantPurgedHandler
{
    public const string Type = NotificationCatalog.TenantPurged;

    public static async Task<OutgoingMessages> HandleAsync(TenantPurgedEvent purged, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            purged.EventId,
            purged.OccurredAt,
            purged.TenantId,
            new Dictionary<string, string> { ["tenantName"] = purged.TenantName },
            await users.FindContactsAsync(await users.ListSystemAdminIdsAsync(cancellationToken), cancellationToken),
            actorId: null,
            users,
            cancellationToken);
}
