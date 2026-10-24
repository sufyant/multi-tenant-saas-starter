using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// The tenant purge ended: the tenant's data is gone and its row is a tombstone (docs/flows/tenant-purge.md). The platform acted. Published
/// through the outbox without a tenant, because the tenant's own log is gone; Audit writes the platform's log only.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="TenantName">The tenant's name, which its tombstone keeps, for its audit entry.</param>
[MessageName("tenant-purged")]
public sealed record TenantPurgedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, string TenantName);
