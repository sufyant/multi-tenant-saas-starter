namespace ControlPlane.Api.Me.Invitations;

/// <summary>The invitation code from the invitation link.</summary>
public sealed record GetInvitationPreviewRequest(string Code);
