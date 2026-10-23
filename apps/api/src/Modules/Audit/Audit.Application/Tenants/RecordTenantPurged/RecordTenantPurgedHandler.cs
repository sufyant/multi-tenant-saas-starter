using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Tenants.RecordTenantPurged;

// The audit entry of a tenant's purge, in the platform's log only: the purge deleted the tenant's own log first
// (docs/flows/tenant-purge.md), and the platform keeps its entries about the tenant for its accountability (GDPR, Article 6(1)(f)). The platform acted (K3).
// The event is handled in a transaction that declares nothing.
public static class RecordTenantPurgedHandler
{
    public static Task HandleAsync(TenantPurgedEvent purged, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(purged.EventId, purged.OccurredAt, AuditActor.Platform, AuditOperations.TenantPurged, AuditTarget.Tenant(purged.TenantId, purged.TenantName)),
            null,
            cancellationToken);
}
