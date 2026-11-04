using ControlPlane.Application.Users;
using ControlPlane.Contracts.DataExports;
using ControlPlane.Domain;
using ControlPlane.Domain.StoredFiles;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.DataExports.DownloadDataExport;

// Only the export's own user finds it: the own_files policy shows no other user's file, so another user's id is not found (OWASP API1).
// An export that is not ready answers 409, and the screen reads its status. Each download records user.data_export_downloaded. The link
// is made after the commit, by GetDownloadLinkQuery, because the file store is an external service and a handler with a transaction calls
// none (section 3).
public static class DownloadDataExportHandler
{
    public static async Task<(Result<DataExportDownload>, OutgoingMessages)> HandleAsync(
        DownloadDataExportCommand command,
        IUserRepository users,
        IStoredFileRepository files,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserIdAsync(command.ActorId, cancellationToken) is not { } userId)
        {
            return (Errors.UserNotFound, []);
        }

        if (await files.FindForUpdateAsync(userId, command.ExportId, cancellationToken) is not { Purpose: StoredFilePurpose.DataExport } export)
        {
            return (Error.NotFound(ControlPlaneErrorCodes.DataExportNotFound, "You have no such export of your data."), []);
        }

        if (export.Status != StoredFileStatus.Ready)
        {
            return (Error.Conflict(ControlPlaneErrorCodes.DataExportNotReady, "The export of your data is not ready to download."), []);
        }

        var now = time.GetUtcNow();
        return (new DataExportDownload(export.ObjectKey, export.FileName), [new DataExportDownloadedEvent(Guid.CreateVersion7(now), now, userId, export.Id)]);
    }
}
