using ControlPlane.Application.Members;
using ControlPlane.Application.Users;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// An invitation as a member sees it: the invited address, the built-in role it gives, the user who invited and their names as
/// they are stored now, when it was created and expires, and its state at the moment it is read (<c>Pending</c>, <c>Expired</c>,
/// <c>Accepted</c>, <c>Cancelled</c>; <see cref="Invitation.StateAt"/>). Inviting and the invitation list answer it.
/// </summary>
public sealed record InvitationView(
    Guid Id,
    string Email,
    RoleView Role,
    Guid InvitedBy,
    UserNameView InvitedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string State)
{
    internal static InvitationView Of(Invitation invitation, UserNameView inviterName, DateTimeOffset now) => new(
        invitation.Id,
        invitation.Email,
        RoleView.Of(BuiltInRoles.All.Single(role => role.Id == invitation.RoleId)),
        invitation.InvitedBy,
        inviterName,
        invitation.CreatedAt,
        invitation.ExpiresAt,
        invitation.StateAt(now).ToString());
}
