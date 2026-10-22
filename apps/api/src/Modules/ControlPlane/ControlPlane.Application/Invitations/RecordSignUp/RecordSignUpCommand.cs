using SharedKernel;

namespace ControlPlane.Application.Invitations.RecordSignUp;

/// <summary>
/// The identity provider's answer: the link to send, and its invitation behind the link, or none for an address with an account.
/// </summary>
[MessageName("record-sign-up")]
public sealed record RecordSignUpCommand(
    Guid TenantId,
    Guid InvitationId,
    string Email,
    string TenantName,
    Uri Link,
    string? SignUpInvitationId,
    DateTimeOffset ExpiresAt);
