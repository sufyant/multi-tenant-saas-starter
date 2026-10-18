namespace ControlPlane.Application.Tenants.ActivateTenant;

/// <summary>Step 2, the pivot: makes the tenant active and announces it, and lets the owner sign up with the accept link they are invited by.</summary>
public sealed record ActivateTenantCommand(Guid TenantId, Guid InvitationId, Uri AcceptLink);
