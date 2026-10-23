using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Users;

namespace Audit.Application.Users.RecordUserDeleted;

// The audit entry of a user's deletion, in the platform's log: a user belongs to no tenant. The system admin who deleted the user acted,
// or the platform, when a tenant's purge left the user without a tenant (K3). The event is handled in a transaction that declares
// nothing, so the entry goes only to the platform's log.
public static class RecordUserDeletedHandler
{
    public static Task HandleAsync(UserDeletedEvent deleted, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(deleted.EventId, deleted.OccurredAt, deleted.DeletedBy is { } deletedBy ? AuditActor.User(deletedBy) : AuditActor.Platform, AuditOperations.UserDeleted, AuditTarget.User(deleted.UserId)),
            null,
            cancellationToken);
}
