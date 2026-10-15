using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Users;

// The audit records of a catalog user's changes, in the platform's log: a user belongs to no tenant. A system admin suspends and
// reactivates a user; the identity provider changes a user's profile and deletes the user (K3). Each event is handled in a transaction
// that declares nothing, so the record goes only to the platform's log.
public static class RecordUserChangesHandler
{
    private const string UserSuspended = "user.suspended";
    private const string UserReactivated = "user.reactivated";
    private const string UserProfileUpdated = "user.profile_updated";
    private const string UserDeleted = "user.deleted";

    public static Task HandleAsync(UserSuspended suspended, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(suspended.EventId, suspended.OccurredAt, AuditActor.User(suspended.SuspendedBy), UserSuspended, AuditTarget.User(suspended.UserId)),
            null,
            cancellationToken);

    public static Task HandleAsync(UserReactivated reactivated, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(reactivated.EventId, reactivated.OccurredAt, AuditActor.User(reactivated.ReactivatedBy), UserReactivated, AuditTarget.User(reactivated.UserId)),
            null,
            cancellationToken);

    public static Task HandleAsync(UserProfileUpdated updated, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(updated.EventId, updated.OccurredAt, AuditActor.IdentityProvider, UserProfileUpdated, AuditTarget.User(updated.UserId)),
            null,
            cancellationToken);

    public static Task HandleAsync(UserDeleted deleted, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(deleted.EventId, deleted.OccurredAt, AuditActor.IdentityProvider, UserDeleted, AuditTarget.User(deleted.UserId)),
            null,
            cancellationToken);
}
