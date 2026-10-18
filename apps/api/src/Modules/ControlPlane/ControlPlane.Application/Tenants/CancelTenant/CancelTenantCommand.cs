namespace ControlPlane.Application.Tenants.CancelTenant;

/// <summary>Compensation before the pivot: cancels the tenant, with a fixed reason code, and its owner's invitation.</summary>
public sealed record CancelTenantCommand(Guid TenantId, Guid InvitationId, string Reason);
