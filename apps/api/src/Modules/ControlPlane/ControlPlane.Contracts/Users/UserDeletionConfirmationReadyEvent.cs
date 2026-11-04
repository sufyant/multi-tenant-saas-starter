using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// A user who deleted their own account is to be told that it is deleted, at the address they had, in their language (GDPR, Article 12(3)).
/// Notifications writes the email; it is this message's one handler, so its retries may be scheduled (W5). Published through the outbox,
/// without a tenant. It carries the erased address until Wolverine removes it (W13), as <c>EraseFormerMemberCommand</c> does.
/// </summary>
[MessageName("user-deletion-confirmation-ready")]
public sealed record UserDeletionConfirmationReadyEvent(Guid UserId, string Email, Language Language);
