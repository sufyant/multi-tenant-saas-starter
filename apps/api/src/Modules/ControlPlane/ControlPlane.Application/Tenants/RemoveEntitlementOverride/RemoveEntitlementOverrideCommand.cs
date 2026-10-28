using SharedKernel;

namespace ControlPlane.Application.Tenants.RemoveEntitlementOverride;

/// <summary>
/// A system admin removes the override of one key of the tenant the platform route names, expired or not: the plan's value applies again.
/// It runs inside that tenant, with the tenant locked.
/// </summary>
[MessageName("remove-entitlement-override")]
public sealed record RemoveEntitlementOverrideCommand(string ActorId, string Key);
