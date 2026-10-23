using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// A system admin deleted a user, or the platform did, after a tenant's purge left the user without a tenant: the user has no membership
/// left, and ControlPlane erased their email address and names (A8). Published through the outbox, without a tenant.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the deleted user.</param>
/// <param name="DeletedBy">The user id of the system admin who deleted the user, or null when the platform did, after a tenant's purge.</param>
[MessageName("user-deleted")]
public sealed record UserDeletedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid? DeletedBy);
