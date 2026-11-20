using ControlPlane.Application.Tenants.GetTenantPlan;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.ExtendTenantTrial;

// K6a: the tenant row is locked first, so the trial's end runs before or after it: a trial that ended is not extended. Rejections come
// first (W7). The command sets a state (I1): the end the trial has announces nothing. The new end and its reminder are scheduled in the same
// transaction; the messages of the old end stay and do nothing (K4). The plan does not change, so the Activity log period does not either.
public static class ExtendTenantTrialHandler
{
    public static async Task<(Result<TenantPlanView>, OutgoingMessages)> HandleAsync(
        ExtendTenantTrialCommand command,
        ITenantRepository tenants,
        ITenantQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        var oldEndsAt = tenant.TrialEndsAt;
        var extended = tenant.ExtendTrial(command.EndsAt);
        if (!extended.IsSuccess)
        {
            return (extended.Error, []);
        }

        var now = time.GetUtcNow();
        await tenants.SaveChangesAsync(cancellationToken);
        var view = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        if (extended.Value == StateChange.Unchanged)
        {
            return (view, []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var newEndsAt = tenant.TrialEndsAt!.Value;
        return (view, [
            new TenantTrialExtendedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, actor, oldEndsAt!.Value, newEndsAt),
            .. TrialMessages.Of(tenant.Id, newEndsAt, now),
        ]);
    }
}
