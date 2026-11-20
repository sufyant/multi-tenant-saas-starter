using ControlPlane.Application.Plans;

namespace ControlPlane.Application.Tenants.GetSignUpTrial;

public static class GetSignUpTrialHandler
{
    public static async Task<SignUpTrialView> HandleAsync(GetSignUpTrialQuery query, TrialOptions trial, IPlanQueries plans, CancellationToken cancellationToken) =>
        new(
            await plans.FindPlanAsync(trial.PlanId, cancellationToken) is { } plan ? new PlanNames(plan.Id, plan.Name) : null,
            (int)Math.Round(trial.Length.TotalDays));
}
