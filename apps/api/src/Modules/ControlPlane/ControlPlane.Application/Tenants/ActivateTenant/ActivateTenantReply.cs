namespace ControlPlane.Application.Tenants.ActivateTenant;

/// <summary>The reply to the saga: the tenant is active (S13).</summary>
public sealed record ActivateTenantReply(Guid TenantId);
