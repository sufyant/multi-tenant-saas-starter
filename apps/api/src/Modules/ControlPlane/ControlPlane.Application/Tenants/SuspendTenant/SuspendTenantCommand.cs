namespace ControlPlane.Application.Tenants.SuspendTenant;

/// <summary>
/// A system admin suspends a tenant, for a reason they write. It runs inside that tenant, which the platform route names, with the tenant
/// locked, so a suspension and a reactivation run one at a time.
/// </summary>
public sealed record SuspendTenantCommand(string ActorId, string Reason);
