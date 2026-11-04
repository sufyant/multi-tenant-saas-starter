namespace ControlPlane.Application.DataExports.FailDataExport;

// An export still in preparation at its timeout failed for good: its step waits in the dead letter queue, whose move logged the error
// (W11). The person can then ask again. A ready or expired export stays as it is.
public static class FailDataExportHandler
{
    public static async Task HandleAsync(FailDataExportCommand command, IStoredFileRepository files, CancellationToken cancellationToken)
    {
        if (await files.FindForUpdateAsync(command.UserId, command.ExportId, cancellationToken) is { } export
            && export.Fail() == SharedKernel.StateChange.Changed)
        {
            await files.SaveChangesAsync(cancellationToken);
        }
    }
}
