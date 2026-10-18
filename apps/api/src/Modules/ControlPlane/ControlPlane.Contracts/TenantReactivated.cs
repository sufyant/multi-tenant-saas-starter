namespace ControlPlane.Contracts;

/// <summary>
/// A system admin reactivated a suspended tenant. Published through the outbox; a subscriber handles it in the tenant's transaction, as
/// the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ReactivatedBy">The catalog user id of the system admin who reactivated the tenant.</param>
/// <param name="TenantName">
/// The tenant's name at that moment, for its audit record. Null only in an event published before the name was added.
/// </param>
public sealed record TenantReactivatedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid ReactivatedBy, string? TenantName);
