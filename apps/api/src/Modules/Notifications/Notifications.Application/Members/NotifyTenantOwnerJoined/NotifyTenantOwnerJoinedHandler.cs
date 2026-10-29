using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Members.NotifyTenantOwnerJoined;

// "The first Owner of a new organisation joined", to every system admin, in the platform's bell.
public static class NotifyTenantOwnerJoinedHandler
{
    public const string Type = NotificationCatalog.TenantOwnerJoined;

    public static async Task<OutgoingMessages> HandleAsync(MemberJoinedEvent joined, IUserDirectory users, CancellationToken cancellationToken)
    {
        if (joined is not { FirstOwner: true, TenantName: { } tenantName }
            || (await users.FindContactsAsync([joined.UserId], cancellationToken)).SingleOrDefault() is not { } owner)
        {
            return [];
        }

        return NotificationFanOut.To(
            Type,
            joined.EventId,
            joined.OccurredAt,
            joined.TenantId,
            new Dictionary<string, string> { ["tenantName"] = tenantName, ["ownerName"] = NotificationFanOut.NameOf(owner) },
            await users.FindContactsAsync(await users.ListSystemAdminIdsAsync(cancellationToken), cancellationToken),
            joined.UserId);
    }
}
