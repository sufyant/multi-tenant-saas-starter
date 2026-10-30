using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Plans.NotifyTenantEntitlementOverrideRemoved;

// "The platform removed an override", with the plan's value that applies again, to the tenant's Owners at that moment, in the tenant's
// bell. An event of the version before step 33-backend-2 has no plan's value, so it tells nobody.
public static class NotifyTenantEntitlementOverrideRemovedHandler
{
    public const string Type = NotificationCatalog.TenantEntitlementOverrideRemoved;

    public static async Task<OutgoingMessages> HandleAsync(EntitlementOverrideRemovedEvent removed, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        removed.PlanValue is { } planValue
            ? await NotificationFanOut.ToAsync(
                Type,
                removed.EventId,
                removed.OccurredAt,
                removed.TenantId,
                new Dictionary<string, string> { ["tenantName"] = removed.TenantName, ["keyName"] = removed.Key, ["value"] = EntitlementValues.Of(planValue) },
                await users.FindContactsAsync(await members.ListOwnerIdsAsync(cancellationToken), cancellationToken),
                removed.RemovedBy,
                users,
                cancellationToken)
            : [];
}
