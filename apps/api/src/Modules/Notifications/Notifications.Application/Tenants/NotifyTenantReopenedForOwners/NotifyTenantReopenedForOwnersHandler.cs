using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantReopenedForOwners;

// "Your organisation was reopened", to the tenant's Owners at that moment, in the personal bell, where they heard of the closure. It leads
// into the organisation, so it keeps the tenant it tells of. The platform acted, so the texts name no system admin.
public static class NotifyTenantReopenedForOwnersHandler
{
    public const string Type = NotificationCatalog.TenantReopenedForOwners;

    public static async Task<OutgoingMessages> HandleAsync(TenantReopenedEvent reopened, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            reopened.EventId,
            reopened.OccurredAt,
            reopened.TenantId,
            new Dictionary<string, string> { ["tenantName"] = reopened.TenantName },
            await users.FindContactsAsync(await members.ListOwnerIdsAsync(cancellationToken), cancellationToken),
            reopened.ReopenedBy,
            users,
            cancellationToken);
}
