using System.Globalization;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantClosed;

// "An organisation was closed", naming who did it and the date its data goes, to every system admin but them, in the platform's bell. An
// event of the version before step 33-backend-2 has no deletion date, so it tells nobody.
public static class NotifyTenantClosedHandler
{
    public const string Type = NotificationCatalog.TenantClosed;

    public static async Task<OutgoingMessages> HandleAsync(TenantClosedEvent closed, IUserDirectory users, CancellationToken cancellationToken) =>
        closed.ReopenableUntil is { } reopenableUntil
            ? await NotificationFanOut.ToAsync(
                Type,
                closed.EventId,
                closed.OccurredAt,
                closed.TenantId,
                new Dictionary<string, string>
                {
                    ["tenantName"] = closed.TenantName,
                    ["actorName"] = NotificationFanOut.PersonOf(closed.ClosedBy),
                    ["reopenableUntil"] = reopenableUntil.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                },
                await users.FindContactsAsync(await users.ListSystemAdminIdsAsync(cancellationToken), cancellationToken),
                closed.ClosedBy,
                users,
                cancellationToken)
            : [];
}
