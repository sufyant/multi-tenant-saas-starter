using Billing.Contracts.Subscriptions;
using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Plans;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.ChangeBilledPlan;

// Billing's change of the tenant's plan, when the tenant's subscription pays for a plan (PlanPaidEvent, B6), as ChangeTenantPlanHandler's
// for a system admin: the tenant row is locked first (K6a), the plan's values apply at once and nothing is deleted when the tenant is then
// over a limit (E4). The plan it is on announces nothing (I1). The member who paid acted, so the event names them (ByMember). The event
// names a shorter Activity log period, as the platform's change does (K14). An event handler answers nobody, so a refusal changes nothing
// and returns no message (W7).
public static class ChangeBilledPlanHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        PlanPaidEvent paid,
        ITenantRepository tenants,
        ITenantQueries queries,
        IPlanRepository plans,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // A tenant that the purge deleted, or a plan that a system admin deleted since, changes nothing.
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant
            || await plans.FindPlanNameAsync(paid.PlanId, cancellationToken) is not { } plan)
        {
            return [];
        }

        var oldPlan = await plans.FindPlanNameAsync(tenant.PlanId, cancellationToken)
            ?? throw new InvalidOperationException("A tenant is always on a plan.");
        // A closed, purging or purged tenant's plan is read-only (E2), so its subscription changes nothing here.
        // During a trial, the plan the tenant returns to keeps the trial; another plan ends it (Tenant.PayForPlan).
        var now = time.GetUtcNow();
        var changed = tenant.PayForPlan(plan.Id, now);
        if (!changed.IsSuccess || changed.Value == StateChange.Unchanged)
        {
            return [];
        }

        var before = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        await tenants.SaveChangesAsync(cancellationToken);
        var after = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        // A payment for the trial's own plan ends the trial and keeps the plan: no change of plan (step 40d).
        if (plan.Id == oldPlan.Id)
        {
            return [];
        }

        return [new TenantPlanChangedEvent(
            Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, paid.PaidBy, oldPlan.Id, oldPlan.Name, plan.Id, plan.Name,
            RetentionShortenings.Between(before, after, now), ByMember: true)];
    }
}
