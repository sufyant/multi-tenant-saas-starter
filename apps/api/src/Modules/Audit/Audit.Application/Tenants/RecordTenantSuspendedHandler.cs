using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

// The audit records of a tenant's suspension, written when ControlPlane announces it, under that tenant. The system admin who suspended
// the tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3).
public static class RecordTenantSuspendedHandler
{
    private const string TenantSuspended = "tenant.suspended";

    public static Task HandleAsync(TenantSuspended suspended, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(suspended.TenantId, suspended.TenantName);
        return log.RecordAsync(
            new PlatformAuditEntry(suspended.EventId, suspended.OccurredAt, AuditActor.User(suspended.SuspendedBy), TenantSuspended, tenant),
            new AuditEntry(suspended.EventId, suspended.OccurredAt, AuditActor.Platform, TenantSuspended, tenant),
            cancellationToken);
    }
}
