namespace ControlPlane.Contracts;

/// <summary>
/// The identity provider changed a catalog user's email address or name, and the catalog's copy took the change (A7). Published
/// through the outbox, without a tenant. It carries no name or address: an audit record holds none (K3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The catalog user id of the user.</param>
public sealed record UserProfileUpdated(Guid EventId, DateTimeOffset OccurredAt, Guid UserId);
