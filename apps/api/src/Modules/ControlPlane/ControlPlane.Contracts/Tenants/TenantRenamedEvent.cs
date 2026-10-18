namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A system admin gave a tenant a new name. Published through the outbox; a subscriber handles it in the tenant's transaction, as the
/// tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="RenamedBy">The catalog user id of the system admin who renamed the tenant.</param>
/// <param name="OldName">The name the tenant had.</param>
/// <param name="NewName">The name the tenant has now.</param>
public sealed record TenantRenamedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid RenamedBy, string OldName, string NewName);
