using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Tenants.RecordTenantSuspended;

// The audit entries of a tenant's suspension, written when ControlPlane announces it, under that tenant. The system admin who suspended
// the tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3). The reason is the
// system admin's: only the platform's entry holds it, because a member never sees it, and the tenant's Owners and Admins read the tenant's log.
public static class RecordTenantSuspendedHandler
{
    public static Task HandleAsync(TenantSuspendedEvent suspended, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(suspended.TenantId, suspended.TenantName);
        return log.RecordAsync(
            new PlatformAuditEntry(
                suspended.EventId,
                suspended.OccurredAt,
                AuditActor.User(suspended.SuspendedBy),
                AuditOperations.TenantSuspended,
                tenant,
                JsonSerializer.Serialize(new TenantSuspendedDetails(suspended.Reason), JsonSerializerOptions.Web)),
            new TenantAuditEntry(suspended.EventId, suspended.OccurredAt, AuditActor.Platform, AuditOperations.TenantSuspended, tenant),
            cancellationToken);
    }
}
