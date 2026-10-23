using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Tenants.RecordTenantPurgeWarned;

// The audit entry of the warning before a tenant's purge, in the tenant's own log, by the platform (K3): the tenant's Owners and Admins
// see that the platform warned them. The platform's log records the purge itself (tenant.purged). Written in the tenant's transaction.
public static class RecordTenantPurgeWarnedHandler
{
    public static Task HandleAsync(TenantPurgeWarnedEvent warned, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(warned.EventId, warned.OccurredAt, AuditActor.Platform, AuditOperations.TenantPurgeWarned, AuditTarget.Tenant(warned.TenantId, warned.TenantName)),
            cancellationToken);
}
