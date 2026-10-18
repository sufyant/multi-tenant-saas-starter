using Wolverine;

namespace ControlPlane.Application.Tenants.TenantCreation;

/// <summary>The invitation email took too long (S10).</summary>
public sealed record InvitationEmailTimedOut(Guid TenantId, TimeSpan DelayTime) : TimeoutMessage(DelayTime);
