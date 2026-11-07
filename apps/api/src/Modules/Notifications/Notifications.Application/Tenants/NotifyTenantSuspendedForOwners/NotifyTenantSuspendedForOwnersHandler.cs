using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantSuspendedForOwners;

// "Your organisation was suspended", to the tenant's Owners at that moment, in the personal bell: the tenant door refuses them now. The
// Owners are read in the tenant's transaction, which the event's envelope declares. The platform acted, so the texts name no system admin,
// and the reason stays out. It leads to the start page.
public static class NotifyTenantSuspendedForOwnersHandler
{
    public const string Type = NotificationCatalog.TenantSuspendedForOwners;

    public static async Task<OutgoingMessages> HandleAsync(TenantSuspendedEvent suspended, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            suspended.EventId,
            suspended.OccurredAt,
            tenantId: null,
            new Dictionary<string, string> { ["tenantName"] = suspended.TenantName },
            await users.FindContactsAsync(await members.ListOwnerIdsAsync(cancellationToken), cancellationToken),
            suspended.SuspendedBy,
            users,
            cancellationToken);
}
