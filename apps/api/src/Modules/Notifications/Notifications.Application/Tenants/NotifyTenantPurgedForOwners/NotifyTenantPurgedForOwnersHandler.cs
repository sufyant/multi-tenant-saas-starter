using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantPurgedForOwners;

// "Your organisation was deleted", to the Owners the purge read before it took the memberships away, in the personal bell, which the
// purge keeps. It leads to the start page. The platform acted, so nobody is left out. An event of the version before step 37a names no
// Owner, so it tells nobody; nor does a deleted user, whom a purge that left them without a tenant may delete first.
public static class NotifyTenantPurgedForOwnersHandler
{
    public const string Type = NotificationCatalog.TenantPurgedForOwners;

    public static async Task<OutgoingMessages> HandleAsync(TenantPurgedEvent purged, IUserDirectory users, CancellationToken cancellationToken) =>
        purged.OwnerIds is { Count: > 0 } owners
            ? await NotificationFanOut.ToAsync(
                Type,
                purged.EventId,
                purged.OccurredAt,
                tenantId: null,
                new Dictionary<string, string> { ["tenantName"] = purged.TenantName },
                await users.FindContactsAsync(owners, cancellationToken),
                actorId: null,
                users,
                cancellationToken)
            : [];
}
