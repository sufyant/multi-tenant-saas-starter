using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// The platform warned a closed tenant's Owners that its purge comes (docs/flows/tenant-purge.md). Published through the outbox; Audit
/// records it in the tenant's log, in the tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">The warning's own id, the same for every copy of one warning, so a warning that arrives twice records one entry.</param>
/// <param name="TenantName">The tenant's name at that moment, for its audit entry.</param>
[MessageName("tenant-purge-warned")]
public sealed record TenantPurgeWarnedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, string TenantName);
