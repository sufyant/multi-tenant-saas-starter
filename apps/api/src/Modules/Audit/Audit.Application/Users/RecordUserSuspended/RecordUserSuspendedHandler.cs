using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Users;

namespace Audit.Application.Users.RecordUserSuspended;

// The audit entry of a catalog user's suspension, in the platform's log: a user belongs to no tenant. The system admin who suspended the
// user acted (K3). The event is handled in a transaction that declares nothing, so the entry goes only to the platform's log.
public static class RecordUserSuspendedHandler
{
    public static Task HandleAsync(UserSuspendedEvent suspended, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(suspended.EventId, suspended.OccurredAt, AuditActor.User(suspended.SuspendedBy), AuditOperations.UserSuspended, AuditTarget.User(suspended.UserId)),
            null,
            cancellationToken);
}
