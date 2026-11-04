using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// A verification code is to be sent to an address: the new address of a change, or the user's own for the deletion of their account, in
/// the user's language. Notifications writes the email; it is this message's one handler, so its retries may be scheduled (W5). Published
/// through the outbox, without a tenant. The code travels in it, and stays in the message store until Wolverine removes the message (W13;
/// section 11, as the invitation link).
/// </summary>
/// <param name="CodeId">The code's id, which the email's idempotency key names.</param>
/// <param name="Code">The six digits; ControlPlane keeps only their hash.</param>
[MessageName("verification-code-ready")]
public sealed record VerificationCodeReadyEvent(
    Guid CodeId, VerificationEmailKind Kind, string Email, Language Language, string Code, DateTimeOffset ExpiresAt);
