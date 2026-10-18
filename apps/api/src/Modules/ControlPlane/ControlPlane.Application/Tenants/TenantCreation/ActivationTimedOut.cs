using Wolverine;

namespace ControlPlane.Application.Tenants.TenantCreation;

/// <summary>The activation took too long (S10).</summary>
public sealed record ActivationTimedOut(Guid TenantId, TimeSpan DelayTime) : TimeoutMessage(DelayTime);
