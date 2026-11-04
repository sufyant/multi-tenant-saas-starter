using ControlPlane.Application.DataExports.DeleteStoredFile;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.DataExports.ExpireDataExport;

// The export expires and its file is deleted after the commit (GDPR, Article 5(1)(e)). The row stays, so the screen says when the file
// was deleted. The bucket's lifecycle rule, which deletes users/*/exports/ after 7 days, is the second guard. A copy finds the export
// expired and does nothing.
public static class ExpireDataExportHandler
{
    public static async Task<OutgoingMessages> HandleAsync(ExpireDataExportCommand command, IStoredFileRepository files, CancellationToken cancellationToken)
    {
        if (await files.FindForUpdateAsync(command.UserId, command.ExportId, cancellationToken) is not { } export
            || export.Expire() == StateChange.Unchanged)
        {
            return [];
        }

        await files.SaveChangesAsync(cancellationToken);
        return [new DeleteStoredFileCommand(export.ObjectKey)];
    }
}
