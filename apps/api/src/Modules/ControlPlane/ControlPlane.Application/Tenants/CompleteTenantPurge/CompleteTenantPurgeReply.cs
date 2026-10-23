using SharedKernel;

namespace ControlPlane.Application.Tenants.CompleteTenantPurge;

/// <summary>ControlPlane purged its data of the tenant and left the tombstone (N3). Only the tenant purge hears it.</summary>
[MessageName("complete-tenant-purge-reply")]
public sealed record CompleteTenantPurgeReply(Guid TenantId);
