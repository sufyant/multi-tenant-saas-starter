using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Users;

namespace Audit.Application.Users.RecordUserSignedUp;

// The audit entry of a sign-up, in the platform's log: a user belongs to no tenant. The new user acted (K3). The event is handled in a
// transaction that declares nothing, so the entry goes only to the platform's log.
public static class RecordUserSignedUpHandler
{
    public static Task HandleAsync(UserSignedUpEvent signedUp, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(signedUp.EventId, signedUp.OccurredAt, AuditActor.User(signedUp.UserId), AuditOperations.UserSignedUp, AuditTarget.User(signedUp.UserId)),
            null,
            cancellationToken);
}
