using SharedKernel;

namespace ControlPlane.Application.Tenants.CloseTenant;

/// <summary>
/// A system admin closes an active or a suspended tenant: its members cannot enter, its data stays, and it can be reopened within the
/// reopen period (K4). It runs inside that tenant, which the platform route names, with the tenant locked.
/// </summary>
[MessageName("close-tenant")]
public sealed record CloseTenantCommand(string ActorId);
