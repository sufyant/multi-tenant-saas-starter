using SharedKernel;

namespace ControlPlane.Application.Tenants.CancelTenant;

/// <summary>The tenant is cancelled.</summary>
[MessageName("cancel-tenant-reply")]
public sealed record CancelTenantReply(Guid TenantId);
