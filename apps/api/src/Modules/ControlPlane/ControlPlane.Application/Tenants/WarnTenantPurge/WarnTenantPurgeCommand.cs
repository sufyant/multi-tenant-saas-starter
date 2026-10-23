using SharedKernel;

namespace ControlPlane.Application.Tenants.WarnTenantPurge;

/// <summary>
/// The purge of a closed tenant comes soon: its Owners are warned, if the tenant is still closed at the closure this message was scheduled
/// for (K4). Scheduled at the closure, for the closure plus the reopen period, less the warning's lead. It runs in the tenant.
/// </summary>
[MessageName("warn-tenant-purge")]
public sealed record WarnTenantPurgeCommand(Guid TenantId, DateTimeOffset ClosedAt);
