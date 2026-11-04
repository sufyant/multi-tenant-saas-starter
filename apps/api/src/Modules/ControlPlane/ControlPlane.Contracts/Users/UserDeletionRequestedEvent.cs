using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// The user asked for the code that deletes their own account (A8). The deletion itself is <see cref="UserDeletedEvent"/>. Published through
/// the outbox, without a tenant.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the user, who acted for themselves.</param>
[MessageName("user-deletion-requested")]
public sealed record UserDeletionRequestedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId);
