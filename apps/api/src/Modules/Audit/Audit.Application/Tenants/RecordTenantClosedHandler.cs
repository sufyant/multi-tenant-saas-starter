using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

// The audit records of a tenant's closure, written when ControlPlane announces it, under that tenant. The system admin who closed the
// tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3).
public static class RecordTenantClosedHandler
{
    public static Task HandleAsync(TenantClosedEvent closed, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(closed.TenantId, closed.TenantName);
        return log.RecordAsync(
            new PlatformAuditEntry(closed.EventId, closed.OccurredAt, AuditActor.User(closed.ClosedBy), AuditOperations.TenantClosed, tenant),
            new AuditEntry(closed.EventId, closed.OccurredAt, AuditActor.Platform, AuditOperations.TenantClosed, tenant),
            cancellationToken);
    }
}
