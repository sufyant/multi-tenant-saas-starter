using SharedKernel;

namespace ControlPlane.Application.DataExports.RequestDataExport;

/// <summary>
/// The signed-in user asks for a copy of their data (GDPR, Articles 15 and 20). The worker prepares it in the background. It runs without
/// a tenant. The token names the user, so a user asks only for their own data.
/// </summary>
[MessageName("request-data-export")]
public sealed record RequestDataExportCommand(string ActorId);
