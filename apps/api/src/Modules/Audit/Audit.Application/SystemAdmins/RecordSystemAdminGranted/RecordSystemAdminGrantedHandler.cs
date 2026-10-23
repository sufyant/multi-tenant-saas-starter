using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.SystemAdmins;

namespace Audit.Application.SystemAdmins.RecordSystemAdminGranted;

// The audit entry of a new system admin, in the platform's log: the system admin list belongs to no tenant. The system admin who
// granted it acted, or the platform, when the system door let in a root system admin (K3). The event is handled in a transaction
// that declares nothing, so the entry goes only to the platform's log.
public static class RecordSystemAdminGrantedHandler
{
    public static Task HandleAsync(SystemAdminGrantedEvent granted, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(
                granted.EventId,
                granted.OccurredAt,
                granted.GrantedBy is { } grantedBy ? AuditActor.User(grantedBy) : AuditActor.Platform,
                AuditOperations.SystemAdminGranted,
                AuditTarget.User(granted.UserId)),
            null,
            cancellationToken);
}
