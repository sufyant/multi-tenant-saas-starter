namespace ControlPlane.Contracts;

/// <summary>A system admin changed a catalog user's names (A7). Published through the outbox, without a tenant.</summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The catalog user id of the renamed user.</param>
/// <param name="RenamedBy">The catalog user id of the system admin who renamed the user.</param>
public sealed record UserRenamed(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid RenamedBy);
