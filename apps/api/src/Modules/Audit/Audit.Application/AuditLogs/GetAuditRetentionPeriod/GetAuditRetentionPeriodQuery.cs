using SharedKernel;

namespace Audit.Application.AuditLogs.GetAuditRetentionPeriod;

/// <summary>
/// The first read of the tenant's Activity log retention (K14): its effective period at this moment, from ControlPlane. The tenant's id names
/// the tenant to ControlPlane's contract; the route proved it by membership (T2).
/// </summary>
[MessageName("get-audit-retention-period")]
public sealed record GetAuditRetentionPeriodQuery(Guid TenantId);
