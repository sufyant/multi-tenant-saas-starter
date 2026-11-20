using ControlPlane.Application.Plans;
using ControlPlane.Application.Tenants.EndTenantTrial;
using ControlPlane.Application.Tenants.GetTenantPlan;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.GiveTenantTrial;

// K6a: the tenant row is locked first, so the trial's end, a payment or a plan change runs before or after it. Rejections come first (W7).
// The trial's end is scheduled in the same transaction (K4); the event names a shorter Activity log period from the effective values
// before and after the trial's plan (K14).
public static class GiveTenantTrialHandler
{
    public static async Task<(Result<TenantPlanView>, OutgoingMessages)> HandleAsync(
        GiveTenantTrialCommand command,
        ITenantRepository tenants,
        ITenantQueries queries,
        IPlanRepository plans,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var length = Tenant.TrialLengthOf(command.Days);
        if (!length.IsSuccess)
        {
            return (length.Error, []);
        }

        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        if (await plans.FindPlanNameAsync(command.PlanId, cancellationToken) is not { } plan)
        {
            return (Plan.NotFound, []);
        }

        var now = time.GetUtcNow();
        var before = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        var given = tenant.GiveTrial(plan.Id, now, length.Value);
        if (!given.IsSuccess)
        {
            return (given.Error, []);
        }

        await tenants.SaveChangesAsync(cancellationToken);
        var view = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var endsAt = now + length.Value;
        return (view, [
            new TenantTrialGivenEvent(
                Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, actor, plan.Id, plan.Name, endsAt, RetentionShortenings.Between(before, view, now)),
            new EndTenantTrialCommand(tenant.Id, endsAt).ScheduledAt(endsAt),
        ]);
    }
}
