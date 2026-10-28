using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Plans;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Tenants.RecordTenantCreated;

// The audit entries of a tenant's creation, written when ControlPlane announces it, under that tenant. The system admin
// who created the tenant acted: the platform's log names them, and the tenant's own log says only that the platform acted (K3). Both
// entries are written in one transaction, with the plan the tenant got; an event of the version before step 32 names none.
public static class RecordTenantCreatedHandler
{
    public static Task HandleAsync(TenantCreatedEvent created, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(created.TenantId, created.Name);
        var details = created is { PlanId: { } planId, PlanName: { } planName }
            ? JsonSerializer.Serialize(new TenantCreatedDetails(new AuditPlan(planId, planName)), JsonSerializerOptions.Web)
            : null;
        return log.RecordAsync(
            new PlatformAuditEntry(created.EventId, created.OccurredAt, AuditActor.User(created.CreatedBy), AuditOperations.TenantCreated, tenant, details),
            new TenantAuditEntry(created.EventId, created.OccurredAt, AuditActor.Platform, AuditOperations.TenantCreated, tenant, details),
            cancellationToken);
    }
}
