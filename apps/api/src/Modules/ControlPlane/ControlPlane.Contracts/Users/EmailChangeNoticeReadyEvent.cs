using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// The old address of a user who changed their address is to be told so, with the new address masked, in the user's language, so a person
/// whose account someone took can act. Notifications writes the email; it is this message's one handler, so its retries may be scheduled
/// (W5). Published through the outbox, without a tenant. It carries both addresses until Wolverine removes it (W13).
/// </summary>
/// <param name="CodeId">The code that proved the new address, which the email's idempotency key names.</param>
[MessageName("email-change-notice-ready")]
public sealed record EmailChangeNoticeReadyEvent(Guid UserId, Guid CodeId, string OldEmail, string NewEmail, Language Language);
