using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Members;

namespace Audit.Application.Members.RecordMembersExported;

// The audit entry of an export of the members, in the tenant's own log, under that tenant: the member who exported them acted, and the
// tenant is the target, with its name of that moment (K3).
public static class RecordMembersExportedHandler
{
    public static Task HandleAsync(MembersExportedEvent exported, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                exported.EventId,
                exported.OccurredAt,
                AuditActor.User(exported.ExportedBy),
                AuditOperations.MembersExported,
                AuditTarget.Tenant(exported.TenantId, exported.TenantName),
                null),
            cancellationToken);
}
