namespace ControlPlane.Application.Invitations.OpenMemberSignUp;

/// <summary>Opens the sign-up for a person a member invited. Sent with the invitation.</summary>
public sealed record OpenMemberSignUpCommand(Guid TenantId, Guid InvitationId, string Email, string TenantName, Uri AcceptLink, DateTimeOffset ExpiresAt);
