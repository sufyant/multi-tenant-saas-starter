using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.SystemAdmins;

namespace Audit.Application.SystemAdmins.RecordSystemAdminRevoked;

// The audit entry of a revoked system admin, in the platform's log: the system admin list belongs to no tenant. The system admin who
// revoked it acted (K3). The event is handled in a transaction that declares nothing, so the entry goes only to the platform's log.
public static class RecordSystemAdminRevokedHandler
{
    public static Task HandleAsync(SystemAdminRevokedEvent revoked, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(revoked.EventId, revoked.OccurredAt, AuditActor.User(revoked.RevokedBy), AuditOperations.SystemAdminRevoked, AuditTarget.User(revoked.UserId)),
            null,
            cancellationToken);
}
