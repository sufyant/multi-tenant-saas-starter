using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Roles;

namespace Audit.Application.Roles.RecordRoleDeleted;

// The audit entry of a deleted custom role, in the tenant's own log, under that tenant: the member who deleted it acted, and the role is
// the target, with the name it had, which the entry keeps after the role is gone (K3).
public static class RecordRoleDeletedHandler
{
    public static Task HandleAsync(RoleDeletedEvent deleted, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                deleted.EventId,
                deleted.OccurredAt,
                AuditActor.User(deleted.DeletedBy),
                AuditOperations.RoleDeleted,
                AuditTarget.Role(deleted.RoleId, deleted.Name),
                null),
            cancellationToken);
}
