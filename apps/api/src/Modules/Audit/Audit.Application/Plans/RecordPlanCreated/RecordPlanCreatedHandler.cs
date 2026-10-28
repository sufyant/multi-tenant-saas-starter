using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Plans;

namespace Audit.Application.Plans.RecordPlanCreated;

// The audit entry of a new plan, in the platform's log: a plan belongs to no tenant. The system admin who created it acted (K3); the
// target keeps the plan's name of that moment.
public static class RecordPlanCreatedHandler
{
    public static Task HandleAsync(PlanCreatedEvent created, IAuditLog log, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(new PlanCreatedDetails(created.IsDefault, AuditEntitlementValue.Of(created.Entitlements)), JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(created.EventId, created.OccurredAt, AuditActor.User(created.CreatedBy), AuditOperations.PlanCreated, AuditTarget.Plan(created.PlanId, created.Name), details),
            null,
            cancellationToken);
    }
}
