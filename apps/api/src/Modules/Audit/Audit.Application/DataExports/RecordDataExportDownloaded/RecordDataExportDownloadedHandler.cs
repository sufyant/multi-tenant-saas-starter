using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.DataExports;

namespace Audit.Application.DataExports.RecordDataExportDownloaded;

// The audit entry of each download link of a user's ready export, in the platform's log, by the user for themselves (K3). The event is
// handled in a transaction that declares nothing.
public static class RecordDataExportDownloadedHandler
{
    public static Task HandleAsync(DataExportDownloadedEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(recorded.EventId, recorded.OccurredAt, AuditActor.User(recorded.UserId), AuditOperations.UserDataExportDownloaded, AuditTarget.User(recorded.UserId)),
            null,
            cancellationToken);
}
