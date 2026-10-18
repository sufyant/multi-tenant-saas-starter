using Wolverine;

namespace ControlPlane.Application.Tenants.TenantCreation;

/// <summary>The tenant's cancellation took too long (S10).</summary>
public sealed record CancellationTimedOut(Guid TenantId, TimeSpan DelayTime) : TimeoutMessage(DelayTime);
