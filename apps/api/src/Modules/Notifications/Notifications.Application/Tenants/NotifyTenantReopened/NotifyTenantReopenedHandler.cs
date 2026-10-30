using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantReopened;

// "An organisation was reopened", naming who did it, to every system admin but them, in the platform's bell.
public static class NotifyTenantReopenedHandler
{
    public const string Type = NotificationCatalog.TenantReopened;

    public static async Task<OutgoingMessages> HandleAsync(TenantReopenedEvent reopened, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            reopened.EventId,
            reopened.OccurredAt,
            reopened.TenantId,
            new Dictionary<string, string> { ["tenantName"] = reopened.TenantName, ["actorName"] = NotificationFanOut.PersonOf(reopened.ReopenedBy) },
            await users.FindContactsAsync(await users.ListSystemAdminIdsAsync(cancellationToken), cancellationToken),
            reopened.ReopenedBy,
            users,
            cancellationToken);
}
