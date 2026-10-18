using System.Text.Json;
using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

// The audit records of a tenant's suspension, written when ControlPlane announces it, under that tenant. The system admin who suspended
// the tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3). The reason is the
// admin's: only the platform's record holds it, because a member never sees it, and the tenant's Owners and Admins read the tenant's log.
public static class RecordTenantSuspendedHandler
{
    public static Task HandleAsync(TenantSuspended suspended, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(suspended.TenantId, suspended.TenantName);
        return log.RecordAsync(
            new PlatformAuditEntry(
                suspended.EventId,
                suspended.OccurredAt,
                AuditActor.User(suspended.SuspendedBy),
                AuditOperations.TenantSuspended,
                tenant,
                suspended.Reason is { } reason ? JsonSerializer.Serialize(new Details(reason), JsonSerializerOptions.Web) : null),
            new AuditEntry(suspended.EventId, suspended.OccurredAt, AuditActor.Platform, AuditOperations.TenantSuspended, tenant),
            cancellationToken);
    }

    private sealed record Details(string Reason);
}
