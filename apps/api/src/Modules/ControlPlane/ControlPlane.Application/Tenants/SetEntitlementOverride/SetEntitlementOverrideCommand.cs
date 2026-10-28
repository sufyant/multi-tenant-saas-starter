using ControlPlane.Application.Plans;
using SharedKernel;

namespace ControlPlane.Application.Tenants.SetEntitlementOverride;

/// <summary>
/// A system admin gives one key a value for the tenant the platform route names, over its plan's value, until the expiry if there is one,
/// with a reason. It runs inside that tenant, with the tenant locked.
/// </summary>
[MessageName("set-entitlement-override")]
public sealed record SetEntitlementOverrideCommand(string ActorId, string Key, EntitlementValueView Value, DateTimeOffset? ExpiresAt, string Reason);
