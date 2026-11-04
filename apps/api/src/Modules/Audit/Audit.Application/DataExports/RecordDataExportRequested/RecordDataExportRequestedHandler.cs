using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.DataExports;

namespace Audit.Application.DataExports.RecordDataExportRequested;

// The audit entry of a user's request for a copy of their data, in the platform's log, by the user for themselves (K3). The event is
// handled in a transaction that declares nothing.
public static class RecordDataExportRequestedHandler
{
    public static Task HandleAsync(DataExportRequestedEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(recorded.EventId, recorded.OccurredAt, AuditActor.User(recorded.UserId), AuditOperations.UserDataExportRequested, AuditTarget.User(recorded.UserId)),
            null,
            cancellationToken);
}
