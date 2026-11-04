using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Users;

namespace Audit.Application.Users.RecordUserEmailChanged;

// The audit entry of a user's new address, in the platform's log, by the user for themselves, as user.renamed (K3). The entry holds
// no address: neither the old nor the new one (K3, A8). The event is handled in a transaction that declares nothing.
public static class RecordUserEmailChangedHandler
{
    public static Task HandleAsync(UserEmailChangedEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(recorded.EventId, recorded.OccurredAt, AuditActor.User(recorded.UserId), AuditOperations.UserEmailChanged, AuditTarget.User(recorded.UserId)),
            null,
            cancellationToken);
}
