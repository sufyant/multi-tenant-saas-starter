using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.Application.Plans.GetPlan;

public static class GetPlanHandler
{
    public static async Task<Result<PlanView>> HandleAsync(GetPlanQuery query, IPlanQueries plans, CancellationToken cancellationToken) =>
        await plans.FindPlanAsync(query.PlanId, cancellationToken) is { } plan ? plan : Plan.NotFound;
}
