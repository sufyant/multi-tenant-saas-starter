using SharedKernel;

namespace ControlPlane.Application.Plans.ListPlans;

public static class ListPlansHandler
{
    public static async Task<Result<ListPage<PlanListItemView>>> HandleAsync(ListPlansQuery query, IPlanQueries plans, CancellationToken cancellationToken) =>
        await plans.ListPlansAsync(query.List, cancellationToken);
}
