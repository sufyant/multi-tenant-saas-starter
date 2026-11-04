using SharedKernel;

namespace ControlPlane.Application.DataExports.ExpireDataExport;

/// <summary>
/// The end of a ready export's lifetime, scheduled when it became ready. It checks on arrival whether the export is still ready, because
/// nothing cancels it (section 7).
/// </summary>
[MessageName("expire-data-export")]
public sealed record ExpireDataExportCommand(Guid ExportId, Guid UserId);
