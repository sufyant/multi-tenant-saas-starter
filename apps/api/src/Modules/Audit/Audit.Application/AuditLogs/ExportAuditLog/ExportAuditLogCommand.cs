using SharedKernel;

namespace Audit.Application.AuditLogs.ExportAuditLog;

/// <summary>A member who reads the tenant's log exports it to a file; the command records it, and the web's server writes the file.</summary>
/// <param name="TenantId">The tenant of the route, which the tenant door proved (T1, T2).</param>
[MessageName("export-audit-log")]
public sealed record ExportAuditLogCommand(string ActorId, Guid TenantId);
