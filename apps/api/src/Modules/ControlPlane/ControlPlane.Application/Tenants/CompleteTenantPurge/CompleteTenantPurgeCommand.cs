using SharedKernel;

namespace ControlPlane.Application.Tenants.CompleteTenantPurge;

/// <summary>
/// The last step of the tenant purge, once every module replied: ControlPlane deletes its data of the tenant, leaves the tenant's
/// tombstone, records the purge and deletes the users left without a tenant (section 6, "Tenant purge"). It runs in the tenant.
/// </summary>
[MessageName("complete-tenant-purge")]
public sealed record CompleteTenantPurgeCommand(Guid TenantId);
