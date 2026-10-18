using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Users;

// The audit record of a catalog user's new names, in the platform's log: a user belongs to no tenant. The system admin who renamed the
// user acted (K3). The event is handled in a transaction that declares nothing, so the record goes only to the platform's log.
public static class RecordUserRenamedHandler
{
    public static Task HandleAsync(UserRenamedEvent renamed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(renamed.EventId, renamed.OccurredAt, AuditActor.User(renamed.RenamedBy), AuditOperations.UserRenamed, AuditTarget.User(renamed.UserId)),
            null,
            cancellationToken);
}
