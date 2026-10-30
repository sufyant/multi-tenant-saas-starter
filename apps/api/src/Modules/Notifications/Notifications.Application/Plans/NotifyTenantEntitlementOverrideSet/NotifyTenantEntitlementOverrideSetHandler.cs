using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Plans.NotifyTenantEntitlementOverrideSet;

// "The platform set a key for your organisation", with its value and, for an override with an end, the moment the plan's value applies
// again, to the tenant's Owners at that moment, in the tenant's bell. The reason is the platform's own note and stays out.
public static class NotifyTenantEntitlementOverrideSetHandler
{
    public const string Type = NotificationCatalog.TenantEntitlementOverrideSet;

    public static async Task<OutgoingMessages> HandleAsync(EntitlementOverrideSetEvent set, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = set.TenantName,
            ["keyName"] = set.Key,
            ["value"] = EntitlementValues.Of(set.NewValue),
        };
        if (set.NewExpiresAt is { } expiresAt)
        {
            values["expiresAt"] = expiresAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }

        return await NotificationFanOut.ToAsync(
            Type,
            set.EventId,
            set.OccurredAt,
            set.TenantId,
            values,
            await users.FindContactsAsync(await members.ListOwnerIdsAsync(cancellationToken), cancellationToken),
            set.SetBy,
            users,
            cancellationToken);
    }
}
