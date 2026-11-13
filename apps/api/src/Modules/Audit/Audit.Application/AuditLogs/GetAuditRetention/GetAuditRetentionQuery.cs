using SharedKernel;

namespace Audit.Application.AuditLogs.GetAuditRetention;

/// <summary>
/// The active tenant's Activity log retention for its screen (K14): the effective period that the first read found, in days, none for no
/// limit, and the hold that the daily job would find at this moment.
/// </summary>
[MessageName("get-audit-retention")]
public sealed record GetAuditRetentionQuery(int? Days);
