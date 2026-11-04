using SharedKernel;

namespace ControlPlane.Application.DataExports.FailDataExport;

/// <summary>
/// The timeout of an export's preparation, scheduled when the person asked. It checks on arrival whether the export is still in
/// preparation, because nothing cancels it (section 7).
/// </summary>
[MessageName("fail-data-export")]
public sealed record FailDataExportCommand(Guid ExportId, Guid UserId);
