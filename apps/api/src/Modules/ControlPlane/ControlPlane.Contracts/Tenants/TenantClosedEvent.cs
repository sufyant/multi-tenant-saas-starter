using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A system admin closed a tenant. Published through the outbox; a subscriber handles it in the tenant's transaction, as the tenant's id
/// travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ClosedBy">The user id of the system admin who closed the tenant.</param>
/// <param name="TenantName">The tenant's name at that moment, for its audit entry.</param>
/// <param name="ReopenableUntil">The end of the reopen period, when the purge deletes the tenant's data, for a notification. None in an
/// event of the version before step 33-backend-2.</param>
[MessageName("tenant-closed")]
public sealed record TenantClosedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid ClosedBy,
    string TenantName,
    DateTimeOffset? ReopenableUntil = null);
