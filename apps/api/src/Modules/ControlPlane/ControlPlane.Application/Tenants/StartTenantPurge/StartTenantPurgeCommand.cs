using SharedKernel;

namespace ControlPlane.Application.Tenants.StartTenantPurge;

/// <summary>
/// The reopen period of a closed tenant is over: the purge starts, if the tenant is still closed at the closure this message was scheduled
/// for (K4). Scheduled at the closure, for the closure plus the reopen period. It runs in the tenant.
/// </summary>
[MessageName("start-tenant-purge")]
public sealed record StartTenantPurgeCommand(Guid TenantId, DateTimeOffset ClosedAt);
