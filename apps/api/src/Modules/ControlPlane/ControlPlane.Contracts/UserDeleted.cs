namespace ControlPlane.Contracts;

/// <summary>
/// A system admin deleted a catalog user: the user has no membership left, and the catalog erased their email address and names (A8).
/// Published through the outbox, without a tenant.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The catalog user id of the deleted user.</param>
/// <param name="DeletedBy">The catalog user id of the system admin who deleted the user.</param>
public sealed record UserDeleted(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid DeletedBy);
