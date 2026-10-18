using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.TenantCreation;

/// <summary>The invitation email took too long (S10).</summary>
[MessageName("invitation-email-timed-out")]
public sealed record InvitationEmailTimedOut(Guid TenantId, TimeSpan DelayTime) : TimeoutMessage(DelayTime);
