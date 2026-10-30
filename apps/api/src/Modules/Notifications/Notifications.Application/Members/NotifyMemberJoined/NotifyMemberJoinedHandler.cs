using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Members.NotifyMemberJoined;

// "A person you invited joined", with the role they joined as, to the member who invited, while still a member: the acceptance names them
// only then. The platform's invitations name nobody, so they tell no member; tenant.owner_joined tells the system admins. An event of the
// version before step 33-backend-2 has no role, so it tells nobody.
public static class NotifyMemberJoinedHandler
{
    public const string Type = NotificationCatalog.MemberJoined;

    public static async Task<OutgoingMessages> HandleAsync(MemberJoinedEvent joined, IUserDirectory users, CancellationToken cancellationToken) =>
        joined is { InvitedBy: { } inviterId, TenantName: { } tenantName, Role: { } role }
            ? await NotificationFanOut.ToAsync(
                Type,
                joined.EventId,
                joined.OccurredAt,
                joined.TenantId,
                new Dictionary<string, string>
                {
                    ["tenantName"] = tenantName,
                    ["memberName"] = NotificationFanOut.PersonOf(joined.UserId),
                    ["roleName"] = role,
                    ["roleNameBuiltIn"] = joined.RoleBuiltIn ? "true" : "false",
                },
                await users.FindContactsAsync([inviterId], cancellationToken),
                joined.UserId,
                users,
                cancellationToken)
            : [];
}
