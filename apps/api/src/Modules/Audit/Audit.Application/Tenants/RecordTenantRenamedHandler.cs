using System.Text.Json;
using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

// The audit records of a tenant's new name, written when ControlPlane announces it, under that tenant. The system admin who renamed the
// tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3). The target keeps the
// name of that moment, the new one; the details hold the old and the new name in both logs.
public static class RecordTenantRenamedHandler
{
    public static Task HandleAsync(TenantRenamedEvent renamed, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(renamed.TenantId, renamed.NewName);
        var details = JsonSerializer.Serialize(new Details(renamed.OldName, renamed.NewName), JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(renamed.EventId, renamed.OccurredAt, AuditActor.User(renamed.RenamedBy), AuditOperations.TenantRenamed, tenant, details),
            new AuditEntry(renamed.EventId, renamed.OccurredAt, AuditActor.Platform, AuditOperations.TenantRenamed, tenant, details),
            cancellationToken);
    }

    private sealed record Details(string OldName, string NewName);
}
