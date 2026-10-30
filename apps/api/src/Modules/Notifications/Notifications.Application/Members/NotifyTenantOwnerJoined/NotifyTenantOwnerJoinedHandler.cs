using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Members.NotifyTenantOwnerJoined;

// "The first Owner of a new organisation joined", to every system admin, in the platform's bell. Audit records the same event as
// member.joined; this type keeps a name of its own, because other people hear of it (U9).
public static class NotifyTenantOwnerJoinedHandler
{
    public const string Type = NotificationCatalog.TenantOwnerJoined;

    public static async Task<OutgoingMessages> HandleAsync(MemberJoinedEvent joined, IUserDirectory users, CancellationToken cancellationToken) =>
        joined is { FirstOwner: true, TenantName: { } tenantName }
            ? await NotificationFanOut.ToAsync(
                Type,
                joined.EventId,
                joined.OccurredAt,
                joined.TenantId,
                new Dictionary<string, string> { ["tenantName"] = tenantName, ["ownerName"] = NotificationFanOut.PersonOf(joined.UserId) },
                await users.FindContactsAsync(await users.ListSystemAdminIdsAsync(cancellationToken), cancellationToken),
                joined.UserId,
                users,
                cancellationToken)
            : [];
}
