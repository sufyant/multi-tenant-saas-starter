using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>A system admin changed a user's names (A7). Published through the outbox, without a tenant.</summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the renamed user.</param>
/// <param name="RenamedBy">The user id of the system admin who renamed the user.</param>
[MessageName("user-renamed")]
public sealed record UserRenamedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid RenamedBy);
