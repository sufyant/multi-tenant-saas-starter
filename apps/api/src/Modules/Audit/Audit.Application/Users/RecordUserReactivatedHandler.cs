using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Users;

// The audit entry of a catalog user's reactivation, in the platform's log: a user belongs to no tenant. The system admin who reactivated
// the user acted (K3). The event is handled in a transaction that declares nothing, so the entry goes only to the platform's log.
public static class RecordUserReactivatedHandler
{
    public static Task HandleAsync(UserReactivatedEvent reactivated, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(reactivated.EventId, reactivated.OccurredAt, AuditActor.User(reactivated.ReactivatedBy), AuditOperations.UserReactivated, AuditTarget.User(reactivated.UserId)),
            null,
            cancellationToken);
}
