using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Plans;

namespace Audit.Application.Plans.RecordTenantPlanChanged;

// The audit entries of a tenant's new plan, written when ControlPlane announces it, under that tenant. The system admin who changed it
// acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3). A member who paid for the plan
// through billing acted in the tenant, so both logs name them (step 40). Both logs hold the old and the new plan. A move to the default plan
// after an unpaid grace is nobody's act: both logs name the platform, under its own operation (B11).
// A shorter Activity log period starts the tenant's hold, after the entry, in the same transaction (K14).
public static class RecordTenantPlanChangedHandler
{
    public static async Task HandleAsync(TenantPlanChangedEvent changed, IAuditLog log, IRetentionStore retention, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(changed.TenantId, changed.TenantName);
        if (changed.Unpaid)
        {
            var moved = JsonSerializer.Serialize(
                new TenantMovedToDefaultPlanDetails(new AuditPlan(changed.OldPlanId, changed.OldPlanName), new AuditPlan(changed.NewPlanId, changed.NewPlanName)),
                JsonSerializerOptions.Web);
            var recordedMove = await log.RecordAsync(
                new PlatformAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.Platform, AuditOperations.TenantMovedToDefaultPlan, tenant, moved),
                new TenantAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.Platform, AuditOperations.TenantMovedToDefaultPlan, tenant, moved),
                cancellationToken);
            await RetentionHolds.StartAsync(retention, recordedMove, changed.RetentionShortened, changed.OccurredAt, cancellationToken);
            return;
        }

        var details = JsonSerializer.Serialize(
            new TenantPlanChangedDetails(new AuditPlan(changed.OldPlanId, changed.OldPlanName), new AuditPlan(changed.NewPlanId, changed.NewPlanName)),
            JsonSerializerOptions.Web);
        var recorded = await log.RecordAsync(
            new PlatformAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.User(changed.ChangedBy), AuditOperations.TenantPlanChanged, tenant, details),
            new TenantAuditEntry(
                changed.EventId, changed.OccurredAt, changed.ByMember ? AuditActor.User(changed.ChangedBy) : AuditActor.Platform, AuditOperations.TenantPlanChanged, tenant, details),
            cancellationToken);
        await RetentionHolds.StartAsync(retention, recorded, changed.RetentionShortened, changed.OccurredAt, cancellationToken);
    }
}
