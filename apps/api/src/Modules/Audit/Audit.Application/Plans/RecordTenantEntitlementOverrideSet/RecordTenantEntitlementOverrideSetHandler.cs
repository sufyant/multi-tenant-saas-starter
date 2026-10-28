using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Plans;

namespace Audit.Application.Plans.RecordTenantEntitlementOverrideSet;

// The audit entry of an override set for a tenant, in the platform's log only: its reason is the provider's own note, which a member
// never reads. The target is the tenant, with its name of that moment.
public static class RecordTenantEntitlementOverrideSetHandler
{
    public static Task HandleAsync(EntitlementOverrideSetEvent set, IAuditLog log, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(
            new TenantEntitlementOverrideSetDetails(
                set.Key,
                set.OldValue is { } old ? AuditEntitlementValue.Of(old) : null,
                set.OldExpiresAt,
                AuditEntitlementValue.Of(set.NewValue),
                set.NewExpiresAt,
                set.Reason),
            JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(set.EventId, set.OccurredAt, AuditActor.User(set.SetBy), AuditOperations.TenantEntitlementOverrideSet, AuditTarget.Tenant(set.TenantId, set.TenantName), details),
            null,
            cancellationToken);
    }
}
