using System.Globalization;
using ControlPlane.Contracts.FeatureFlags;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.FeatureFlags.NotifyFeatureFlagsReadyToRemove;

// The weekly count of the flags ready to remove from the code, release flags on for everyone for 30 days, to every
// system admin, in the platform's bell (3.07). The event's id is its week's, so the week's copies tell each system admin once (U3). A
// week without such a flag tells nobody.
public static class NotifyFeatureFlagsReadyToRemoveHandler
{
    public const string Type = NotificationCatalog.FeatureFlagsReadyToRemove;

    public static async Task<OutgoingMessages> HandleAsync(FeatureFlagCleanupDueEvent due, IUserDirectory users, CancellationToken cancellationToken) =>
        due.ReadyToRemoveCount is > 0 and var count
            ? await NotificationFanOut.ToAsync(
                Type,
                due.EventId,
                due.OccurredAt,
                tenantId: null,
                new Dictionary<string, string> { ["count"] = count.ToString(CultureInfo.InvariantCulture) },
                await users.FindContactsAsync(await users.ListSystemAdminIdsAsync(cancellationToken), cancellationToken),
                actorId: null,
                users,
                cancellationToken)
            : [];
}
