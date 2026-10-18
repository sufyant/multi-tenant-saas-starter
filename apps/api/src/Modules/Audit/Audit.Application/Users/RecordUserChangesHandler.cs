using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Users;

// The audit records of a catalog user's changes, in the platform's log: a user belongs to no tenant. A system admin suspends, reactivates,
// renames and deletes a user, and a user changes their own preferences (K3). Each event is handled in a transaction that declares
// nothing, so the record goes only to the platform's log.
public static class RecordUserChangesHandler
{
    public static Task HandleAsync(UserSuspended suspended, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(suspended.EventId, suspended.OccurredAt, AuditActor.User(suspended.SuspendedBy), AuditOperations.UserSuspended, AuditTarget.User(suspended.UserId)),
            null,
            cancellationToken);

    public static Task HandleAsync(UserReactivated reactivated, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(reactivated.EventId, reactivated.OccurredAt, AuditActor.User(reactivated.ReactivatedBy), AuditOperations.UserReactivated, AuditTarget.User(reactivated.UserId)),
            null,
            cancellationToken);

    public static Task HandleAsync(UserRenamed renamed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(renamed.EventId, renamed.OccurredAt, AuditActor.User(renamed.RenamedBy), AuditOperations.UserRenamed, AuditTarget.User(renamed.UserId)),
            null,
            cancellationToken);

    public static Task HandleAsync(UserDeleted deleted, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(deleted.EventId, deleted.OccurredAt, AuditActor.User(deleted.DeletedBy), AuditOperations.UserDeleted, AuditTarget.User(deleted.UserId)),
            null,
            cancellationToken);

    // The user acted for themselves. The record holds no value: it says that the preferences changed, not to what.
    public static Task HandleAsync(UserPreferencesChanged changed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.User(changed.UserId), AuditOperations.UserPreferencesChanged, AuditTarget.User(changed.UserId)),
            null,
            cancellationToken);
}
