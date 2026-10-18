using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Tenants.RecordTenantReactivated;

// The audit entries of a tenant's reactivation, written when ControlPlane announces it, under that tenant. The system admin who
// reactivated the tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3).
public static class RecordTenantReactivatedHandler
{
    public static Task HandleAsync(TenantReactivatedEvent reactivated, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(reactivated.TenantId, reactivated.TenantName);
        return log.RecordAsync(
            new PlatformAuditEntry(reactivated.EventId, reactivated.OccurredAt, AuditActor.User(reactivated.ReactivatedBy), AuditOperations.TenantReactivated, tenant),
            new TenantAuditEntry(reactivated.EventId, reactivated.OccurredAt, AuditActor.Platform, AuditOperations.TenantReactivated, tenant),
            cancellationToken);
    }
}
