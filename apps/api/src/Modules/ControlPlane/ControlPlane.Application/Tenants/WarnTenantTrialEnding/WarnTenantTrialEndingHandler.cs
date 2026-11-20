using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace ControlPlane.Application.Tenants.WarnTenantTrialEnding;

// The reminder of a trial's end (step 40d; 13.16). It locks the tenant row (K6a), so an extension or the end runs before or after it, and
// tells only of a trial that still ends at the message's end: an extension cancels no scheduled message, so the message itself checks (K4).
// Nobody acted, so the event names nobody. The event's id comes from the tenant and the end, so a copy that arrives again tells nobody twice:
// the notification's id is the event's (U3).
public static class WarnTenantTrialEndingHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        WarnTenantTrialEndingCommand warn,
        ITenantRepository tenants,
        IPlanRepository plans,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant || !tenant.IsInTrialUntil(warn.EndsAt))
        {
            return [];
        }

        var trialPlan = await plans.FindPlanNameAsync(tenant.PlanId, cancellationToken)
            ?? throw new InvalidOperationException("A tenant is always on a plan.");
        var nextPlan = await plans.FindPlanNameAsync(tenant.PlanBeforeTrialId!.Value, cancellationToken)
            ?? throw new InvalidOperationException("A plan is never deleted.");
        var now = time.GetUtcNow();
        return [new TenantTrialEndNearedEvent(
            TrialMessages.WarningIdOf(tenant.Id, warn.EndsAt), now, tenant.Id, tenant.Name, trialPlan.Name, nextPlan.Name, warn.EndsAt, TrialMessages.WarningDays)];
    }
}
