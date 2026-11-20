using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.EndTenantTrial;

// The end of a tenant's trial (step 40d). It locks the tenant row (K6a), so a payment or an extension runs before or after it, and acts only
// for a trial that still ends at the message's end: an extension or an earlier end cancels no scheduled message, so the message itself
// checks (K4). The plan's values apply at once and nothing is deleted when the tenant is then over a limit (E4). Nobody acted, so the event
// names nobody. A copy that arrives again finds no trial, and writes nothing.
public static class EndTenantTrialHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        EndTenantTrialCommand end,
        ITenantRepository tenants,
        ITenantQueries queries,
        IPlanRepository plans,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant || !tenant.IsInTrialUntil(end.EndsAt))
        {
            return [];
        }

        var trialPlan = await plans.FindPlanNameAsync(tenant.PlanId, cancellationToken)
            ?? throw new InvalidOperationException("A tenant is always on a plan.");
        var now = time.GetUtcNow();
        var before = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        if (tenant.EndTrial(now) == StateChange.Unchanged)
        {
            return [];
        }

        var newPlan = await plans.FindPlanNameAsync(tenant.PlanId, cancellationToken)
            ?? throw new InvalidOperationException("A tenant is always on a plan.");
        await tenants.SaveChangesAsync(cancellationToken);
        var after = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        return [new TenantTrialEndedEvent(
            Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, trialPlan.Id, trialPlan.Name, newPlan.Id, newPlan.Name,
            RetentionShortenings.Between(before, after, now))];
    }
}
