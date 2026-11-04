using ControlPlane.Application.DataExports.FailDataExport;
using ControlPlane.Application.DataExports.PrepareDataExport;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.DataExports;
using ControlPlane.Domain;
using ControlPlane.Domain.StoredFiles;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.DataExports.RequestDataExport;

// One export is in progress at a time: an export still in preparation answers 409. The command locks the user row, so two requests at
// once run one at a time and the second sees the first (K6l). Rejections come first (W7). In the same transaction it adds the export, the
// event that Audit records, the worker's step, and the export's timeout, which fails an export that is still in preparation an hour later
// (docs/flows/personal-data-export.md).
public static class RequestDataExportHandler
{
    public static async Task<(Result<DataExportView>, OutgoingMessages)> HandleAsync(
        RequestDataExportCommand command,
        IUserRepository users,
        IStoredFileRepository files,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.ActorId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, []);
        }

        if (await files.FindLatestDataExportStatusAsync(user.Id, cancellationToken) == StoredFileStatus.Preparing)
        {
            return (Error.Conflict(ControlPlaneErrorCodes.DataExportInProgress, "An export of your data is in preparation."), []);
        }

        var now = time.GetUtcNow();
        var export = StoredFile.PrepareDataExport(Guid.CreateVersion7(now), user.Id, now);
        files.Add(export);
        await files.SaveChangesAsync(cancellationToken);

        return (
            DataExportView.Of(export),
            [
                new DataExportRequestedEvent(Guid.CreateVersion7(now), now, user.Id, export.Id),
                new PrepareDataExportCommand(export.Id, user.Id),
                new FailDataExportCommand(export.Id, user.Id).ScheduledAt(now + StoredFile.DataExportPreparationTimeout),
            ]);
    }
}
