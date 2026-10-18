using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>A system admin suspended a user (A9). Published through the outbox, without a tenant.</summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the suspended user.</param>
/// <param name="SuspendedBy">The user id of the system admin who suspended the user.</param>
[MessageName("user-suspended")]
public sealed record UserSuspendedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid SuspendedBy);
