using SharedKernel;

namespace ControlPlane.Application.DataExports.GetMyDataExport;

/// <summary>The signed-in user's latest data export. It runs without a tenant.</summary>
[MessageName("get-my-data-export")]
public sealed record GetMyDataExportQuery(string ActorId);
