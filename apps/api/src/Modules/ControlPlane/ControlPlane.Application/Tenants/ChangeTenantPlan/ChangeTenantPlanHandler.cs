using ControlPlane.Application.Plans;
using ControlPlane.Application.Tenants.GetTenantPlan;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.ChangeTenantPlan;

// K6a: the tenant row is locked first, so a limit that a tenant command checks under the same lock sees the plan before or after the
// change, never between. The command sets a state (I1): the plan the tenant is on announces nothing. Rejections come first (W7).
public static class ChangeTenantPlanHandler
{
    public static async Task<(Result<TenantPlanView>, OutgoingMessages)> HandleAsync(
        ChangeTenantPlanCommand command,
        ITenantRepository tenants,
        ITenantQueries queries,
        IPlanRepository plans,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        if (await plans.FindPlanNameAsync(command.PlanId, cancellationToken) is not { } plan)
        {
            return (Plan.NotFound, []);
        }

        var oldPlan = await plans.FindPlanNameAsync(tenant.PlanId, cancellationToken)
            ?? throw new InvalidOperationException("A tenant is always on a plan.");
        var changed = tenant.ChangePlan(plan.Id);
        if (!changed.IsSuccess)
        {
            return (changed.Error, []);
        }

        var now = time.GetUtcNow();
        await tenants.SaveChangesAsync(cancellationToken);
        var view = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        if (changed.Value == StateChange.Unchanged)
        {
            return (view, []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        return (view, [new TenantPlanChangedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, actor, oldPlan.Id, oldPlan.Name, plan.Id, plan.Name)]);
    }
}
