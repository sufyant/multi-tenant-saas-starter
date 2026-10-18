namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A system admin reopened a closed tenant. Published through the outbox; a subscriber handles it in the tenant's transaction, as the
/// tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ReopenedBy">The catalog user id of the system admin who reopened the tenant.</param>
/// <param name="TenantName">The tenant's name at that moment, for its audit entry.</param>
public sealed record TenantReopenedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid ReopenedBy, string TenantName);
