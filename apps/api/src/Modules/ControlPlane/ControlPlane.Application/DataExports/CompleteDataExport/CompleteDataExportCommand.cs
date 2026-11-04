using SharedKernel;

namespace ControlPlane.Application.DataExports.CompleteDataExport;

/// <summary>The worker saved the export's file; ControlPlane marks the export ready and tells the user.</summary>
[MessageName("complete-data-export")]
public sealed record CompleteDataExportCommand(Guid ExportId, Guid UserId, long SizeBytes);
