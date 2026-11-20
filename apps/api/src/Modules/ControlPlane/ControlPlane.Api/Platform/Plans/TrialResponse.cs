using ControlPlane.Application.Tenants;

namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// The tenant's last trial (step 40d): its plan, when it started, and when it ends, or ended; while it runs, <c>returnsTo</c>, the plan the
/// tenant returns to when it ends, and null after its end. The platform (4.28, 4.29) and the web (10.08) read it.
/// </summary>
public sealed record TrialResponse(PlanNameResponse Plan, DateTimeOffset StartedAt, DateTimeOffset EndsAt, PlanNameResponse? ReturnsTo)
{
    internal static TrialResponse? Of(TrialView? trial) =>
        trial is null
            ? null
            : new(
                new PlanNameResponse(trial.Plan.Id, trial.Plan.Name),
                trial.StartedAt,
                trial.EndsAt,
                trial.ReturnsTo is { } returnsTo ? new PlanNameResponse(returnsTo.Id, returnsTo.Name) : null);
}
