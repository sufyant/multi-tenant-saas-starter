using System.Globalization;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantClosedForOwners;

// "Your organisation was closed", with the date its data goes, to the tenant's Owners at that moment, in the personal bell: the tenant door
// refuses them now. It leads to the start page. The platform acted, so the texts name no system admin. An event of the version before step
// 33-backend-2 has no deletion date, so it tells nobody.
public static class NotifyTenantClosedForOwnersHandler
{
    public const string Type = NotificationCatalog.TenantClosedForOwners;

    public static async Task<OutgoingMessages> HandleAsync(TenantClosedEvent closed, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        closed.ReopenableUntil is { } reopenableUntil
            ? await NotificationFanOut.ToAsync(
                Type,
                closed.EventId,
                closed.OccurredAt,
                tenantId: null,
                new Dictionary<string, string>
                {
                    ["tenantName"] = closed.TenantName,
                    ["reopenableUntil"] = reopenableUntil.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                },
                await users.FindContactsAsync(await members.ListOwnerIdsAsync(cancellationToken), cancellationToken),
                closed.ClosedBy,
                users,
                cancellationToken)
            : [];
}
