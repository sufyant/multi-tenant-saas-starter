using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantSuspended;

// "An organisation was suspended", naming who did it, to every system admin but them, in the platform's bell. The reason stays out.
public static class NotifyTenantSuspendedHandler
{
    public const string Type = NotificationCatalog.TenantSuspended;

    public static async Task<OutgoingMessages> HandleAsync(TenantSuspendedEvent suspended, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            suspended.EventId,
            suspended.OccurredAt,
            suspended.TenantId,
            new Dictionary<string, string> { ["tenantName"] = suspended.TenantName, ["actorName"] = NotificationFanOut.PersonOf(suspended.SuspendedBy) },
            await users.FindContactsAsync(await users.ListSystemAdminIdsAsync(cancellationToken), cancellationToken),
            suspended.SuspendedBy,
            users,
            cancellationToken);
}
