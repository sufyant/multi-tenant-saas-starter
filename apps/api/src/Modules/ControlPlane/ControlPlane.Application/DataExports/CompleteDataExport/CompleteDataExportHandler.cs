using ControlPlane.Application.DataExports.DeleteStoredFile;
using ControlPlane.Application.DataExports.ExpireDataExport;
using ControlPlane.Contracts.DataExports;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.DataExports.CompleteDataExport;

// The export is ready until the end of its lifetime. In the same transaction it tells the user, through Notifications, and schedules the
// expiry, which deletes the file (docs/flows/personal-data-export.md). A copy finds the export ready and does nothing (S7). An export
// whose row is gone, because the user was deleted meanwhile, or that failed by its timeout, keeps no file: the file is deleted after the
// commit.
public static class CompleteDataExportHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        CompleteDataExportCommand command,
        IStoredFileRepository files,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var key = FileKeys.UserExport(command.UserId, command.ExportId);
        if (await files.FindForUpdateAsync(command.UserId, command.ExportId, cancellationToken) is not { } export)
        {
            return [new DeleteStoredFileCommand(key)];
        }

        var now = time.GetUtcNow();
        if (export.Ready(command.SizeBytes, now) == StateChange.Unchanged)
        {
            return export.Status == Domain.Files.StoredFileStatus.Ready ? [] : [new DeleteStoredFileCommand(key)];
        }

        await files.SaveChangesAsync(cancellationToken);
        return
        [
            new DataExportReadyEvent(export.Id, now, export.UserId, export.ExpiresAt!.Value),
            new ExpireDataExportCommand(export.Id, export.UserId).ScheduledAt(export.ExpiresAt!.Value),
        ];
    }
}
