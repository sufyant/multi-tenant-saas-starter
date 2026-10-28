using SharedKernel;

namespace ControlPlane.Application.Tenants.ChangeTenantPlan;

/// <summary>
/// A system admin puts the tenant the platform route names on a plan. It runs inside that tenant, with the tenant locked. The plan's values
/// apply at once; nothing is deleted when the tenant is then over a limit.
/// </summary>
[MessageName("change-tenant-plan")]
public sealed record ChangeTenantPlanCommand(string ActorId, Guid PlanId);
