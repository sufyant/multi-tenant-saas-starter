namespace ControlPlane.Contracts;

/// <summary>
/// A system admin reactivated a suspended tenant. Published through the outbox; a subscriber handles it in the tenant's transaction, as
/// the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ReactivatedBy">The catalog user id of the system admin who reactivated the tenant.</param>
public sealed record TenantReactivated(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid ReactivatedBy);
