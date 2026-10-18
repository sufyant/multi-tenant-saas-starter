namespace ControlPlane.Application.Tenants.TenantCreation;

/// <summary>How long the tenant creation waits for the activation, the invitation email and the cancellation (S10).</summary>
public sealed record TenantCreationTimeouts(TimeSpan Activation, TimeSpan InvitationEmail, TimeSpan Cancellation);
