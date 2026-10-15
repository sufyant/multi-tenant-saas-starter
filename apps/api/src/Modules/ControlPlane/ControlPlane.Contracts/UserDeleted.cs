namespace ControlPlane.Contracts;

/// <summary>
/// The identity provider deleted a catalog user, and the catalog erased its copy of their profile (A8). Published through the outbox,
/// without a tenant.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The catalog user id of the deleted user.</param>
public sealed record UserDeleted(Guid EventId, DateTimeOffset OccurredAt, Guid UserId);
