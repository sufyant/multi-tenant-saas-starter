using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Plans.NotifyTenantPlanChanged;

// "The organisation's plan changed", with the old plan and the new one, to the tenant's Owners at that moment, in the tenant's bell. The
// Owners are read in the tenant's transaction, which the event's envelope declares, so a member who becomes an Owner later sees no older
// one. A plan's edit tells nobody: the platform's audit log records it.
public static class NotifyTenantPlanChangedHandler
{
    public const string Type = NotificationCatalog.TenantPlanChanged;

    public static async Task<OutgoingMessages> HandleAsync(TenantPlanChangedEvent changed, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            changed.EventId,
            changed.OccurredAt,
            changed.TenantId,
            new Dictionary<string, string> { ["tenantName"] = changed.TenantName, ["oldPlanName"] = changed.OldPlanName, ["newPlanName"] = changed.NewPlanName },
            await users.FindContactsAsync(await members.ListOwnerIdsAsync(cancellationToken), cancellationToken),
            changed.ChangedBy,
            users,
            cancellationToken);
}
