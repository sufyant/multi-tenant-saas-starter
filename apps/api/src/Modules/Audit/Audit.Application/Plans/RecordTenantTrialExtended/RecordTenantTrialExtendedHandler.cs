using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Plans.RecordTenantTrialExtended;

// The audit entries of a trial that a system admin extended, written when ControlPlane announces it, under that tenant (step 40d). The
// platform's log names the system admin; the tenant's own log says only that the platform acted (K3). The plan stays, so does the period.
public static class RecordTenantTrialExtendedHandler
{
    public static async Task HandleAsync(TenantTrialExtendedEvent extended, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(extended.TenantId, extended.TenantName);
        var details = JsonSerializer.Serialize(new TenantTrialExtendedDetails(extended.OldEndsAt, extended.NewEndsAt), JsonSerializerOptions.Web);
        await log.RecordAsync(
            new PlatformAuditEntry(extended.EventId, extended.OccurredAt, AuditActor.User(extended.ExtendedBy), AuditOperations.TenantTrialExtended, tenant, details),
            new TenantAuditEntry(extended.EventId, extended.OccurredAt, AuditActor.Platform, AuditOperations.TenantTrialExtended, tenant, details),
            cancellationToken);
    }
}
