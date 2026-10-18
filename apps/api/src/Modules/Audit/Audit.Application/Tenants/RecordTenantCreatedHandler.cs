using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

// The audit records of a tenant's creation, written when ControlPlane announces the tenant active, under that tenant. The system admin
// who created the tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3). Both
// records are written in one transaction.
public static class RecordTenantCreatedHandler
{
    public static Task HandleAsync(TenantActivated activated, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(activated.TenantId, activated.Name);
        return log.RecordAsync(
            new PlatformAuditEntry(activated.EventId, activated.OccurredAt, AuditActor.User(activated.CreatedBy), AuditOperations.TenantCreated, tenant),
            new AuditEntry(activated.EventId, activated.OccurredAt, AuditActor.Platform, AuditOperations.TenantCreated, tenant),
            cancellationToken);
    }
}
