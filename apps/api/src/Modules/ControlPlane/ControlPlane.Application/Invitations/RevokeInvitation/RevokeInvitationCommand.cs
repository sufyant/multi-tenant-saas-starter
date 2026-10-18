namespace ControlPlane.Application.Invitations.RevokeInvitation;

/// <summary>
/// An Owner or an Admin revokes a pending invitation of the active tenant; its link stops working at once (K2), and the identity provider's
/// invitation behind it is withdrawn after the commit.
/// </summary>
public sealed record RevokeInvitationCommand(string ActorId, Guid InvitationId);
