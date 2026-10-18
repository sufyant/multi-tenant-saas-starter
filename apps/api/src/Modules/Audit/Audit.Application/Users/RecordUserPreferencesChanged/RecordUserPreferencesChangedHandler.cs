using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Users;

namespace Audit.Application.Users.RecordUserPreferencesChanged;

// The audit entry of a user's new preferences, in the platform's log: a user belongs to no tenant. The user acted for
// themselves (K3). The entry holds no value: it says that the preferences changed, not to what. The event is handled in a transaction
// that declares nothing, so the entry goes only to the platform's log.
public static class RecordUserPreferencesChangedHandler
{
    public static Task HandleAsync(UserPreferencesChangedEvent changed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.User(changed.UserId), AuditOperations.UserPreferencesChanged, AuditTarget.User(changed.UserId)),
            null,
            cancellationToken);
}
