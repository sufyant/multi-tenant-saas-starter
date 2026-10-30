using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Members.NotifyMemberRemoved;

// "You were removed from an organisation", to the removed person, naming who removed them. It is personal, about no tenant, because the
// person can no longer enter it; the email always goes (a required type, U4). A member who removed themselves is not told. Only a member
// removes a member: a user's deletion publishes no MemberRemovedEvent. An event of the version before step 33 has no tenant name, so it
// tells nobody.
public static class NotifyMemberRemovedHandler
{
    public const string Type = NotificationCatalog.MemberRemoved;

    public static async Task<OutgoingMessages> HandleAsync(MemberRemovedEvent removed, IUserDirectory users, CancellationToken cancellationToken) =>
        removed.TenantName is { } tenantName
            ? await NotificationFanOut.ToAsync(
                Type,
                removed.EventId,
                removed.OccurredAt,
                tenantId: null,
                new Dictionary<string, string> { ["tenantName"] = tenantName, ["actorName"] = NotificationFanOut.PersonOf(removed.RemovedBy) },
                await users.FindContactsAsync([removed.UserId], cancellationToken),
                removed.RemovedBy,
                users,
                cancellationToken)
            : [];
}
