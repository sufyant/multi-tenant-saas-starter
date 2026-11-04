using SharedKernel;

namespace ControlPlane.Application.DataExports.DeleteStoredFile;

// The handler takes no DbContext, so Wolverine opens no transaction around the call. A failure retries (W11), then the message waits in the
// dead letter queue; the bucket's lifecycle rule deletes an export's file all the same. A file already gone is done.
public static class DeleteStoredFileHandler
{
    public static Task HandleAsync(DeleteStoredFileCommand command, IFileStore files, CancellationToken cancellationToken) =>
        files.DeleteAsync(command.ObjectKey, cancellationToken);
}
