using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Users;

namespace Audit.Application.Users.RecordUserDeletionRequested;

// The audit entry of a user's request for the code that deletes their own account, in the platform's log, by the user for themselves
// (K3). The deletion itself is user.deleted. The event is handled in a transaction that declares nothing.
public static class RecordUserDeletionRequestedHandler
{
    public static Task HandleAsync(UserDeletionRequestedEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(recorded.EventId, recorded.OccurredAt, AuditActor.User(recorded.UserId), AuditOperations.UserDeletionRequested, AuditTarget.User(recorded.UserId)),
            null,
            cancellationToken);
}
