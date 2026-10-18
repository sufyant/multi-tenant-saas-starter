namespace ControlPlane.Contracts;

/// <summary>
/// A system admin suspended a tenant. Published through the outbox; a subscriber handles it in the tenant's transaction, as the tenant's
/// id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="SuspendedBy">The catalog user id of the system admin who suspended the tenant.</param>
/// <param name="TenantName">
/// The tenant's name at that moment, for its audit record. Null only in an event published before the name was added.
/// </param>
/// <param name="Reason">
/// The system admin's reason, for the platform's audit record only: a member never sees it. Null only in an event published before the
/// reason was added.
/// </param>
public sealed record TenantSuspendedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid SuspendedBy, string? TenantName, string? Reason);
