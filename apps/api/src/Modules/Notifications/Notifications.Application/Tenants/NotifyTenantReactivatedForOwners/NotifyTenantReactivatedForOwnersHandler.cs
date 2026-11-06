using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantReactivatedForOwners;

// "Your organisation was reactivated", to the tenant's Owners at that moment, in the personal bell, where they heard of the suspension. It
// leads into the organisation, so it keeps the tenant it tells of. The platform acted, so the texts name no system admin.
public static class NotifyTenantReactivatedForOwnersHandler
{
    public const string Type = NotificationCatalog.TenantReactivatedForOwners;

    public static async Task<OutgoingMessages> HandleAsync(TenantReactivatedEvent reactivated, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            reactivated.EventId,
            reactivated.OccurredAt,
            reactivated.TenantId,
            new Dictionary<string, string> { ["tenantName"] = reactivated.TenantName },
            await users.FindContactsAsync(await members.ListOwnerIdsAsync(cancellationToken), cancellationToken),
            reactivated.ReactivatedBy,
            users,
            cancellationToken);
}
