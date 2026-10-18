namespace ControlPlane.Application.Invitations.ListInvitations;

/// <summary>The fields the invitation list sorts by. The first is the default: the one that expires first comes first.</summary>
public enum ListInvitationsSort
{
    ExpiresAt,
    Email,
}
