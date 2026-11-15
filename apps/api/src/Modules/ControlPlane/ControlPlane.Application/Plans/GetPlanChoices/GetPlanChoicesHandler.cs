namespace ControlPlane.Application.Plans.GetPlanChoices;

public static class GetPlanChoicesHandler
{
    public static Task<IReadOnlyList<PlanView>> HandleAsync(GetPlanChoicesQuery query, IPlanQueries plans, CancellationToken cancellationToken) =>
        plans.GetPlanChoicesAsync(cancellationToken);
}
