using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Tenants.RecordTenantCreated;

// The audit entries of a tenant's creation, written when ControlPlane announces it, under that tenant. The system admin
// who created the tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3). Both
// entries are written in one transaction.
public static class RecordTenantCreatedHandler
{
    public static Task HandleAsync(TenantCreatedEvent created, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(created.TenantId, created.Name);
        return log.RecordAsync(
            new PlatformAuditEntry(created.EventId, created.OccurredAt, AuditActor.User(created.CreatedBy), AuditOperations.TenantCreated, tenant),
            new TenantAuditEntry(created.EventId, created.OccurredAt, AuditActor.Platform, AuditOperations.TenantCreated, tenant),
            cancellationToken);
    }
}
