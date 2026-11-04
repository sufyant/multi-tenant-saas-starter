using ControlPlane.Application.DataExports.CompleteDataExport;
using ControlPlane.Domain.Files;
using SharedKernel;

namespace ControlPlane.Application.DataExports.PrepareDataExport;

// The handler takes no DbContext, so Wolverine opens no transaction around the calls: each module's exporter reads in a transaction of its
// own module (W1), and the file store is an external service. A failure retries (W11). The file's key is the export's, so a second try
// replaces the first file. Then ControlPlane marks the export ready, in a transaction of its own.
public static class PrepareDataExportHandler
{
    public static async Task<CompleteDataExportCommand> HandleAsync(
        PrepareDataExportCommand command,
        IEnumerable<IPersonalDataExporter> exporters,
        IFileStore files,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var parts = new List<PersonalDataPart>();
        foreach (var exporter in exporters)
        {
            parts.AddRange(await exporter.ExportAsync(command.UserId, cancellationToken));
        }

        using var archive = DataExportArchive.Write(parts, time.GetUtcNow());
        await files.SaveAsync(FileKeys.UserExport(command.UserId, command.ExportId), archive, StoredFile.ZipContentType, cancellationToken);
        return new CompleteDataExportCommand(command.ExportId, command.UserId, archive.Length);
    }
}
