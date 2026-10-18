namespace ControlPlane.Application.Tenants.CancelTenant;

/// <summary>The tenant is cancelled.</summary>
public sealed record CancelTenantReply(Guid TenantId);
