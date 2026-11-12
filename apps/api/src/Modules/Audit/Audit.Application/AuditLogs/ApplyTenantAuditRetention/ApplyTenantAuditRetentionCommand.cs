using SharedKernel;

namespace Audit.Application.AuditLogs.ApplyTenantAuditRetention;

/// <summary>
/// The first step of one tenant's retention, delivered in that tenant (K14): it reads the tenant's status and period from ControlPlane. The
/// tenant's id names the tenant to ControlPlane's contract; the data is the envelope's tenant's (T2).
/// </summary>
/// <param name="Readings">How many times this run read the period again for an old second step; none at the run's start.</param>
[MessageName("apply-tenant-audit-retention")]
public sealed record ApplyTenantAuditRetentionCommand(Guid TenantId, int Readings = 0);
