using SharedKernel;

namespace ControlPlane.Application.Plans.GetPlan;

/// <summary>One plan, with its values and how many tenants are on it.</summary>
[MessageName("get-plan")]
public sealed record GetPlanQuery(Guid PlanId);
