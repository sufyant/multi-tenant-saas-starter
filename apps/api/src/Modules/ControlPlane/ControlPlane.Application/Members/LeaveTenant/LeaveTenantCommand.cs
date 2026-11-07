using SharedKernel;

namespace ControlPlane.Application.Members.LeaveTenant;

/// <summary>A member leaves the active tenant by themselves; they lose access at once, and the last Owner stays (K1).</summary>
[MessageName("leave-tenant")]
public sealed record LeaveTenantCommand(string ActorId);
