using ControlPlane.Application.Members;
using ControlPlane.Domain.Invitations;
using ControlPlane.Application.Users;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// An invitation as a member sees it: the invited address, the built-in role it gives, the catalog user who invited and their names as
/// the catalog holds them now, when it was created and expires, and its state at the moment it is read (<c>Pending</c>, <c>Expired</c>,
/// <c>Accepted</c>, <c>Cancelled</c>; <see cref="Invitation.StateAt"/>). Inviting and the invitation list answer it.
/// </summary>
public sealed record InvitationSummary(
    Guid Id,
    string Email,
    RoleSummary Role,
    Guid InvitedBy,
    PersonName InvitedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string State)
{
    internal static InvitationSummary Of(Invitation invitation, User inviter, DateTimeOffset now) => new(
        invitation.Id,
        invitation.Email,
        RoleSummary.Of(BuiltInRoles.All.Single(role => role.Id == invitation.RoleId)),
        invitation.InvitedBy,
        PersonName.Of(inviter),
        invitation.CreatedAt,
        invitation.ExpiresAt,
        invitation.StateAt(now).ToString());
}
