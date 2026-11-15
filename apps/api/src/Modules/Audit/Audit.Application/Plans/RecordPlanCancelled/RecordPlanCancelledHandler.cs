using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using Billing.Contracts.Subscriptions;

namespace Audit.Application.Plans.RecordPlanCancelled;

// A member's change of the tenant's period end (B9, 12.12), written when Billing announces it, under that tenant. The member acted in the
// tenant, so both logs name them, with the plans as they were at that moment (K3).
public static class RecordPlanCancelledHandler
{
    public static async Task HandleAsync(PlanCancelledEvent changed, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(changed.TenantId, changed.TenantName);
        var details = JsonSerializer.Serialize(new TenantPlanCancelledDetails(new AuditPlan(changed.PlanId, changed.PlanName), new AuditPlan(changed.NewPlanId, changed.NewPlanName), changed.MovesAt), JsonSerializerOptions.Web);
        await log.RecordAsync(
            new PlatformAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.User(changed.ChangedBy), AuditOperations.TenantPlanCancelled, tenant, details),
            new TenantAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.User(changed.ChangedBy), AuditOperations.TenantPlanCancelled, tenant, details),
            cancellationToken);
    }
}
