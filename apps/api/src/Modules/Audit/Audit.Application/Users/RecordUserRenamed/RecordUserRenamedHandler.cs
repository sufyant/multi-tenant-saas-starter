using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Users;

namespace Audit.Application.Users.RecordUserRenamed;

// The audit entry of a catalog user's new names, in the platform's log: a user belongs to no tenant. The system admin who renamed the
// user acted (K3). The event is handled in a transaction that declares nothing, so the entry goes only to the platform's log.
public static class RecordUserRenamedHandler
{
    public static Task HandleAsync(UserRenamedEvent renamed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(renamed.EventId, renamed.OccurredAt, AuditActor.User(renamed.RenamedBy), AuditOperations.UserRenamed, AuditTarget.User(renamed.UserId)),
            null,
            cancellationToken);
}
