namespace ControlPlane.Application.Tenants.ReactivateTenant;

/// <summary>
/// A system admin gives a suspended tenant back to its members. It runs inside that tenant, which the platform route names, with the
/// tenant locked, so a suspension and a reactivation run one at a time.
/// </summary>
public sealed record ReactivateTenantCommand(string ActorId);
