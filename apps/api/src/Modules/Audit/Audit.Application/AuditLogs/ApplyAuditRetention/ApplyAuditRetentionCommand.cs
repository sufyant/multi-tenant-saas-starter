using SharedKernel;

namespace Audit.Application.AuditLogs.ApplyAuditRetention;

/// <summary>Runs the retention of both audit logs (K14, K15). The worker sends it at its start and every 24 hours. It writes no audit entry (K3).</summary>
[MessageName("apply-audit-retention")]
public sealed record ApplyAuditRetentionCommand;
