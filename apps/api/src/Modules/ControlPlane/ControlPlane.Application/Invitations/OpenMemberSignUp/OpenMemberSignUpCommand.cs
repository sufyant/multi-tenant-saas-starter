using SharedKernel;

namespace ControlPlane.Application.Invitations.OpenMemberSignUp;

/// <summary>Opens the sign-up for a person a member invited. Sent with the invitation.</summary>
[MessageName("open-member-sign-up")]
public sealed record OpenMemberSignUpCommand(Guid TenantId, Guid InvitationId, string Email, string TenantName, Uri AcceptLink, DateTimeOffset ExpiresAt);
