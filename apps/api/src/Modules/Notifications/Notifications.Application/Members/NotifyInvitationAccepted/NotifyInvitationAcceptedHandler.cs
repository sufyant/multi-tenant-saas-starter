using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Members.NotifyInvitationAccepted;

// "An invitation you sent was accepted", to the member who invited, while still a member: the acceptance names them only then. The
// platform's invitations name nobody.
public static class NotifyInvitationAcceptedHandler
{
    public const string Type = NotificationCatalog.InvitationAccepted;

    public static async Task<OutgoingMessages> HandleAsync(MemberJoinedEvent joined, IUserDirectory users, CancellationToken cancellationToken)
    {
        if (joined is not { InvitedBy: { } inviterId, TenantName: { } tenantName })
        {
            return [];
        }

        var people = await users.FindContactsAsync([inviterId, joined.UserId], cancellationToken);
        return people.SingleOrDefault(person => person.UserId == joined.UserId) is { } member
            ? NotificationFanOut.To(
                Type,
                joined.EventId,
                joined.OccurredAt,
                joined.TenantId,
                new Dictionary<string, string> { ["tenantName"] = tenantName, ["memberName"] = NotificationFanOut.NameOf(member) },
                people.Where(person => person.UserId == inviterId),
                joined.UserId)
            : [];
    }
}
