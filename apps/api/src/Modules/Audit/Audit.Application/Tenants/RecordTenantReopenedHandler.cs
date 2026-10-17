using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

// The audit records of a tenant's reopening, written when ControlPlane announces it, under that tenant. The system admin who reopened
// the tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3).
public static class RecordTenantReopenedHandler
{
    private const string TenantReopened = "tenant.reopened";

    public static Task HandleAsync(TenantReopened reopened, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(reopened.TenantId, reopened.TenantName);
        return log.RecordAsync(
            new PlatformAuditEntry(reopened.EventId, reopened.OccurredAt, AuditActor.User(reopened.ReopenedBy), TenantReopened, tenant),
            new AuditEntry(reopened.EventId, reopened.OccurredAt, AuditActor.Platform, TenantReopened, tenant),
            cancellationToken);
    }
}
