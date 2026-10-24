using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// The platform warned a closed tenant's Owners that its purge comes (docs/flows/tenant-purge.md). Published through the outbox; Audit
/// records it in the platform's log and in the tenant's log, in the tenant's transaction, as the tenant's id travels in the message's
/// envelope.
/// </summary>
/// <param name="EventId">The warning's own id, the same for every copy of one warning, so a warning that arrives twice records one entry.</param>
/// <param name="TenantName">The tenant's name at that moment, for its audit entries.</param>
/// <param name="PurgesAt">
/// The moment of the purge that the Owners were warned of: the closure plus the reopen period. Null only in an event of the version
/// before step r12, which a worker of that version can still publish during the deploy (docs/deployment.md, section 8). Expand first:
/// a later release makes it required.
/// </param>
[MessageName("tenant-purge-warned")]
public sealed record TenantPurgeWarnedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, string TenantName, DateTimeOffset? PurgesAt);
