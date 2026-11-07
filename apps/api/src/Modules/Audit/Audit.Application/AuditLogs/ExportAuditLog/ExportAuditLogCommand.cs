using SharedKernel;

namespace Audit.Application.AuditLogs.ExportAuditLog;

/// <summary>A member who reads the tenant's log exports it to a file; the command records it, and the web's server writes the file.</summary>
/// <remarks>The tenant travels in the message's envelope, as for every tenant command (T2).</remarks>
[MessageName("export-audit-log")]
public sealed record ExportAuditLogCommand(string ActorId);
