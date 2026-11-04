using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>The user changed their own email address, with the code sent to the new one (A7). Published through the outbox, without a tenant.</summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the user, who acted for themselves.</param>
[MessageName("user-email-changed")]
public sealed record UserEmailChangedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId);
