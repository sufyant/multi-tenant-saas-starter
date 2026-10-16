using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// An invitation as a member sees it: the invited address, the built-in role it gives, the catalog user who invited, and when it was
/// created and expires. The screen shows an expired invitation from its expiry. Inviting and the invitation list answer it.
/// </summary>
public sealed record InvitationSummary(Guid Id, string Email, string Role, Guid InvitedBy, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt)
{
    internal static InvitationSummary Of(Invitation invitation) => new(
        invitation.Id,
        invitation.Email,
        BuiltInRoles.All.Single(role => role.Id == invitation.RoleId).Name,
        invitation.InvitedBy,
        invitation.CreatedAt,
        invitation.ExpiresAt);
}
