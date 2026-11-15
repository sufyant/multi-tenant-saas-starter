using Billing.Contracts.Subscriptions;
using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Plans;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.MoveToDefaultPlan;

// Billing's move of the tenant to the default plan, when its subscription ended or its unpaid grace ended (PlanEndedEvent, B11), as
// ChangeBilledPlanHandler's for a plan that a member paid for: the tenant row is locked first (K6a), the plan's values apply at once and
// nothing is deleted when the tenant is then over a limit (E4). The default plan it is on announces nothing (I1). The end of a cancelled
// subscription names the member who cancelled it (ByMember); an unpaid one is nobody's act (Unpaid). A closed, purging or purged tenant's plan
// is read-only (E2). An event handler answers nobody, so a refusal changes nothing and returns no message (W7).
public static class MoveToDefaultPlanHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        PlanEndedEvent ended,
        ITenantRepository tenants,
        ITenantQueries queries,
        IPlanRepository plans,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return [];
        }

        var defaultPlan = await plans.FindPlanNameAsync(await plans.FindDefaultPlanIdAsync(cancellationToken), cancellationToken)
            ?? throw new InvalidOperationException("The default plan always exists.");
        var oldPlan = await plans.FindPlanNameAsync(tenant.PlanId, cancellationToken)
            ?? throw new InvalidOperationException("A tenant is always on a plan.");
        var changed = tenant.ChangePlan(defaultPlan.Id);
        if (!changed.IsSuccess || changed.Value == StateChange.Unchanged)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var before = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        await tenants.SaveChangesAsync(cancellationToken);
        var after = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        return [new TenantPlanChangedEvent(
            Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, ended.EndedBy, oldPlan.Id, oldPlan.Name, defaultPlan.Id, defaultPlan.Name,
            RetentionShortenings.Between(before, after, now), ByMember: !ended.Unpaid, Unpaid: ended.Unpaid)];
    }
}
