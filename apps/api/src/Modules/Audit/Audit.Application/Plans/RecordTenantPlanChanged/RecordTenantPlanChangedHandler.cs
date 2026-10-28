using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Plans;

namespace Audit.Application.Plans.RecordTenantPlanChanged;

// The audit entries of a tenant's new plan, written when ControlPlane announces it, under that tenant. The system admin who changed it
// acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3). Both logs hold the old and the
// new plan.
public static class RecordTenantPlanChangedHandler
{
    public static Task HandleAsync(TenantPlanChangedEvent changed, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(changed.TenantId, changed.TenantName);
        var details = JsonSerializer.Serialize(
            new TenantPlanChangedDetails(changed.OldPlanId, changed.OldPlanName, changed.NewPlanId, changed.NewPlanName), JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.User(changed.ChangedBy), AuditOperations.TenantPlanChanged, tenant, details),
            new TenantAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.Platform, AuditOperations.TenantPlanChanged, tenant, details),
            cancellationToken);
    }
}
