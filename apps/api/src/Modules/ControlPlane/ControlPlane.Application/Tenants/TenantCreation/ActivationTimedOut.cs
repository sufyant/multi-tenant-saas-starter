using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.TenantCreation;

/// <summary>The activation took too long (S10).</summary>
[MessageName("activation-timed-out")]
public sealed record ActivationTimedOut(Guid TenantId, TimeSpan DelayTime) : TimeoutMessage(DelayTime);
