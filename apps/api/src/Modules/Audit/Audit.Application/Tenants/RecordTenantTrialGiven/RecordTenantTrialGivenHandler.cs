using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Plans;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Tenants.RecordTenantTrialGiven;

// The audit entries of a trial that a system admin gave, written when ControlPlane announces it, under that tenant (step 40d). The platform's
// log names the system admin; the tenant's own log says only that the platform acted (K3). A shorter Activity log period starts the tenant's
// hold, after the entry, in the same transaction (K14).
public static class RecordTenantTrialGivenHandler
{
    public static async Task HandleAsync(TenantTrialGivenEvent given, IAuditLog log, IRetentionStore retention, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(given.TenantId, given.TenantName);
        var details = JsonSerializer.Serialize(
            new TenantTrialGivenDetails(new AuditPlan(given.TrialPlanId, given.TrialPlanName), given.EndsAt), JsonSerializerOptions.Web);
        var recorded = await log.RecordAsync(
            new PlatformAuditEntry(given.EventId, given.OccurredAt, AuditActor.User(given.GivenBy), AuditOperations.TenantTrialGiven, tenant, details),
            new TenantAuditEntry(given.EventId, given.OccurredAt, AuditActor.Platform, AuditOperations.TenantTrialGiven, tenant, details),
            cancellationToken);
        await RetentionHolds.StartAsync(retention, recorded, given.RetentionShortened, given.OccurredAt, cancellationToken);
    }
}
