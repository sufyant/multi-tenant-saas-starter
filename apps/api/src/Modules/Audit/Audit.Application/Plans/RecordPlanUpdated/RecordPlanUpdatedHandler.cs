using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Plans;

namespace Audit.Application.Plans.RecordPlanUpdated;

// The audit entry of a plan edit, in the platform's log: a plan belongs to no tenant. The details hold the old and the new name, default
// mark and values, and the plan that had the mark when it moved to this plan; the target keeps the new name. A plan edit writes no entry
// in the logs of the tenants on the plan (reading-the-audit-logs.md).
public static class RecordPlanUpdatedHandler
{
    public static Task HandleAsync(PlanUpdatedEvent updated, IAuditLog log, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(
            new PlanUpdatedDetails(
                updated.OldName,
                updated.NewName,
                updated.WasDefault,
                updated.IsDefault,
                AuditEntitlementValue.Of(updated.OldEntitlements),
                AuditEntitlementValue.Of(updated.NewEntitlements),
                updated is { PreviousDefaultPlanId: { } previousId, PreviousDefaultPlanName: { } previousName } ? new AuditPlan(previousId, previousName) : null),
            JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(updated.EventId, updated.OccurredAt, AuditActor.User(updated.UpdatedBy), AuditOperations.PlanUpdated, AuditTarget.Plan(updated.PlanId, updated.NewName), details),
            null,
            cancellationToken);
    }
}
