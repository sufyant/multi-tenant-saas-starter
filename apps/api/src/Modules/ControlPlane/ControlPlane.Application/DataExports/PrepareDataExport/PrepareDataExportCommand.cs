using SharedKernel;

namespace ControlPlane.Application.DataExports.PrepareDataExport;

/// <summary>The worker collects every module's part of the user's data, writes the file and saves it in the file store.</summary>
[MessageName("prepare-data-export")]
public sealed record PrepareDataExportCommand(Guid ExportId, Guid UserId);
