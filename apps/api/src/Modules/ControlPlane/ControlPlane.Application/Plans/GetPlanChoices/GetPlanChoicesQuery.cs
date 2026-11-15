using SharedKernel;

namespace ControlPlane.Application.Plans.GetPlanChoices;

/// <summary>
/// Every plan, for the Change plan page of a member who manages billing (step 40, 10.02): its name, whether it is the default plan, its
/// values, its prices and its meters, the cheapest first. Plans belong to no tenant (R6).
/// </summary>
[MessageName("get-plan-choices")]
public sealed record GetPlanChoicesQuery;
