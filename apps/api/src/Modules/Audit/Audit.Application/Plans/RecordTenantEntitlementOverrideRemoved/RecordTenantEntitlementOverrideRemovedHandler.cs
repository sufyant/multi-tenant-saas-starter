using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Plans;

namespace Audit.Application.Plans.RecordTenantEntitlementOverrideRemoved;

// The audit entries of an override removed from a tenant, in both logs, as its setting.
public static class RecordTenantEntitlementOverrideRemovedHandler
{
    public static Task HandleAsync(EntitlementOverrideRemovedEvent removed, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(removed.TenantId, removed.TenantName);
        var details = JsonSerializer.Serialize(
            new TenantEntitlementOverrideRemovedDetails(removed.Key, AuditEntitlementValue.Of(removed.OldValue), removed.OldExpiresAt), JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(removed.EventId, removed.OccurredAt, AuditActor.User(removed.RemovedBy), AuditOperations.TenantEntitlementOverrideRemoved, tenant, details),
            new TenantAuditEntry(removed.EventId, removed.OccurredAt, AuditActor.Platform, AuditOperations.TenantEntitlementOverrideRemoved, tenant, details),
            cancellationToken);
    }
}
