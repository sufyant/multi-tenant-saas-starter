using Audit.Contracts.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;

namespace Audit.Application.AuditLogs.RecordActivityLogExported;

// The audit entry of an export of the Activity log, in the tenant's own log, under that tenant: the member who exported it acted, and the
// tenant is the target. Audit knows the tenant by its id only, so the target keeps no name.
public static class RecordActivityLogExportedHandler
{
    public static Task HandleAsync(ActivityLogExportedEvent exported, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                exported.EventId,
                exported.OccurredAt,
                AuditActor.User(exported.ExportedBy),
                AuditOperations.ActivityLogExported,
                AuditTarget.Tenant(exported.TenantId, null),
                null),
            cancellationToken);
}
