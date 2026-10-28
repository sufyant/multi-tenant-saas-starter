using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Plans;

namespace Audit.Application.Plans.RecordTenantEntitlementOverrideSet;

// The audit entries of an override set for a tenant, written in the tenant's transaction: the platform's by the system admin, the tenant's
// by the platform (K3). The reason is the provider's own note, so only the platform's entry keeps it, as for tenant.suspended.
public static class RecordTenantEntitlementOverrideSetHandler
{
    public static Task HandleAsync(EntitlementOverrideSetEvent set, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(set.TenantId, set.TenantName);
        var details = new TenantEntitlementOverrideSetDetails(
            set.Key,
            set.OldValue is { } old ? AuditEntitlementValue.Of(old) : null,
            set.OldExpiresAt,
            AuditEntitlementValue.Of(set.NewValue),
            set.NewExpiresAt,
            set.Reason);
        return log.RecordAsync(
            new PlatformAuditEntry(
                set.EventId, set.OccurredAt, AuditActor.User(set.SetBy), AuditOperations.TenantEntitlementOverrideSet, tenant, JsonSerializer.Serialize(details, JsonSerializerOptions.Web)),
            new TenantAuditEntry(
                set.EventId, set.OccurredAt, AuditActor.Platform, AuditOperations.TenantEntitlementOverrideSet, tenant, JsonSerializer.Serialize(details with { Reason = null }, JsonSerializerOptions.Web)),
            cancellationToken);
    }
}
