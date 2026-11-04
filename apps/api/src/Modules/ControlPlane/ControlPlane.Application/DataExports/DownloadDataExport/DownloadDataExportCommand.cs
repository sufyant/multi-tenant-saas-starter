using SharedKernel;

namespace ControlPlane.Application.DataExports.DownloadDataExport;

/// <summary>
/// The signed-in user downloads their ready export. The API checks the person, then gives a link that works for five minutes and is never
/// stored. It runs without a tenant.
/// </summary>
[MessageName("download-data-export")]
public sealed record DownloadDataExportCommand(string ActorId, Guid ExportId);
