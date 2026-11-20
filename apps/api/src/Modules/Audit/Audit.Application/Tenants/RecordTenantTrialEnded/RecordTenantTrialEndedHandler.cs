using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Plans.RecordTenantTrialEnded;

// The audit entries of the end of a tenant's trial, written when ControlPlane announces it, under that tenant (step 40d). Nobody acted: both
// logs name the platform (K3). A shorter Activity log period starts the tenant's hold, after the entry, in the same transaction (K14).
public static class RecordTenantTrialEndedHandler
{
    public static async Task HandleAsync(TenantTrialEndedEvent ended, IAuditLog log, IRetentionStore retention, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(ended.TenantId, ended.TenantName);
        var recorded = await log.RecordAsync(
            new PlatformAuditEntry(ended.EventId, ended.OccurredAt, AuditActor.Platform, AuditOperations.TenantTrialEnded, tenant, null),
            new TenantAuditEntry(ended.EventId, ended.OccurredAt, AuditActor.Platform, AuditOperations.TenantTrialEnded, tenant, null),
            cancellationToken);
        await RetentionHolds.StartAsync(retention, recorded, ended.RetentionShortened, ended.OccurredAt, cancellationToken);
    }
}
