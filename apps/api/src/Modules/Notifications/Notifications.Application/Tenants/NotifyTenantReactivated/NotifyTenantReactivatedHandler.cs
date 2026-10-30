using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantReactivated;

// "An organisation was reactivated", naming who did it, to every system admin but them, in the platform's bell.
public static class NotifyTenantReactivatedHandler
{
    public const string Type = NotificationCatalog.TenantReactivated;

    public static async Task<OutgoingMessages> HandleAsync(TenantReactivatedEvent reactivated, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            reactivated.EventId,
            reactivated.OccurredAt,
            reactivated.TenantId,
            new Dictionary<string, string> { ["tenantName"] = reactivated.TenantName, ["actorName"] = NotificationFanOut.PersonOf(reactivated.ReactivatedBy) },
            await users.FindContactsAsync(await users.ListSystemAdminIdsAsync(cancellationToken), cancellationToken),
            reactivated.ReactivatedBy,
            users,
            cancellationToken);
}
