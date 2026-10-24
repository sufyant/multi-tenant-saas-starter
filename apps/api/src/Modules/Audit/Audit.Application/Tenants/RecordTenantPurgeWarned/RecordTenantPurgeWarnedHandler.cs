using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Tenants;

namespace Audit.Application.Tenants.RecordTenantPurgeWarned;

// The audit entries of the warning before a tenant's purge, written when ControlPlane announces it, under that tenant, in one
// transaction (K3). No person acted: both logs name the platform. The platform's log shows a system admin which tenants were warned, and
// the tenant's own log shows its Owners and Admins. Both hold the moment of the purge they were warned of.
public static class RecordTenantPurgeWarnedHandler
{
    public static Task HandleAsync(TenantPurgeWarnedEvent warned, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(warned.TenantId, warned.TenantName);
        var details = JsonSerializer.Serialize(new TenantPurgeWarnedDetails(warned.PurgesAt), JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(warned.EventId, warned.OccurredAt, AuditActor.Platform, AuditOperations.TenantPurgeWarned, tenant, details),
            new TenantAuditEntry(warned.EventId, warned.OccurredAt, AuditActor.Platform, AuditOperations.TenantPurgeWarned, tenant, details),
            cancellationToken);
    }
}
