using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using Notifications.Contracts.EmailPreferences;

namespace Audit.Application.EmailPreferences.RecordEmailPreferencesChanged;

// The audit entry of a user's new email preferences for notifications, in the platform's log: a user belongs to no tenant. The user acted
// for themselves (K3). The entry holds no value, as the entry of a user's other preferences does.
public static class RecordEmailPreferencesChangedHandler
{
    public static Task HandleAsync(EmailPreferencesChangedEvent changed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.User(changed.UserId), AuditOperations.UserEmailPreferencesChanged, AuditTarget.User(changed.UserId)),
            null,
            cancellationToken);
}
