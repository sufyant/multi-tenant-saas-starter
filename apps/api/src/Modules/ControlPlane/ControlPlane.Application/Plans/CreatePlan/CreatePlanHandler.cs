using ControlPlane.Application.Users;
using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Plans.CreatePlan;

// A plan belongs to no tenant. Marked default, the new plan takes the mark under the mark's lock (K6a). Rejections come first (W7); the
// unique index on lower(name) is the second guard behind the check (K6b).
public static class CreatePlanHandler
{
    public static async Task<(Result<PlanView>, OutgoingMessages)> HandleAsync(
        CreatePlanCommand command,
        IPlanRepository plans,
        IPlanQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var values = EntitlementValueView.Read(command.Entitlements);
        if (!values.IsSuccess)
        {
            return (values.Error, []);
        }

        var now = time.GetUtcNow();
        var nameTaken = await plans.IsNameTakenAsync(command.Name.Trim(), null, cancellationToken);
        var created = Plan.Create(Guid.CreateVersion7(now), command.Name, values.Value, nameTaken, now);
        if (!created.IsSuccess)
        {
            return (created.Error, []);
        }

        var plan = created.Value;
        plans.Add(plan);
        if (command.IsDefault)
        {
            await plans.SaveChangesAsync(cancellationToken);
            (await plans.FindDefaultPlanForUpdateAsync(cancellationToken)).Mark(plan.Id, isDefault: true);
        }

        await plans.SaveChangesAsync(cancellationToken);
        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var view = await queries.FindPlanAsync(plan.Id, cancellationToken) ?? throw new InvalidOperationException("The plan was added in this transaction.");
        return (view, [new PlanCreatedEvent(Guid.CreateVersion7(now), now, plan.Id, actor, plan.Name, command.IsDefault, EntitlementValueView.RecordsOf(plan.ValueMap))]);
    }
}
