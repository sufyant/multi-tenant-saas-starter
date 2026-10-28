using ControlPlane.Application.Users;
using ControlPlane.Contracts.Plans;
using ControlPlane.Domain;
using ControlPlane.Domain.Plans;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Plans.UpdatePlan;

// K6a: the plan row is locked first, then the default mark, so two edits of one plan, and two plans that take the mark, run one after the
// other. Every rejection comes before any change (W7): the default plan keeps its mark, so the check runs before the edit. The edit and
// the mark set states (I1): what the plan has already announces nothing. The cache of every tenant on the plan follows the plan's tag (C5).
public static class UpdatePlanHandler
{
    public static async Task<(Result<PlanView>, OutgoingMessages)> HandleAsync(
        UpdatePlanCommand command,
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

        if (await plans.FindPlanForUpdateAsync(command.PlanId, cancellationToken) is not { } plan)
        {
            return (Plan.NotFound, []);
        }

        var mark = await plans.FindDefaultPlanForUpdateAsync(cancellationToken);
        var wasDefault = mark.PlanId == plan.Id;
        if (wasDefault && !command.IsDefault)
        {
            return (Error.Conflict(ControlPlaneErrorCodes.PlanDefaultRequired, "The default plan keeps its mark until another plan takes it."), []);
        }

        var nameTaken = await plans.IsNameTakenAsync(command.Name.Trim(), plan.Id, cancellationToken);
        var edited = plan.Edit(command.Name, values.Value, nameTaken);
        if (!edited.IsSuccess)
        {
            return (edited.Error, []);
        }

        mark.Mark(plan.Id, command.IsDefault);
        var before = edited.Value;
        var changed = before.OldName != plan.Name || wasDefault != command.IsDefault || !SameValues(before.OldValues, plan.ValueMap);
        await plans.SaveChangesAsync(cancellationToken);
        var view = await queries.FindPlanAsync(plan.Id, cancellationToken) ?? throw new InvalidOperationException("The plan was locked in this transaction.");
        if (!changed)
        {
            return (view, []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var now = time.GetUtcNow();
        return (view, [new PlanUpdatedEvent(
            Guid.CreateVersion7(now),
            now,
            plan.Id,
            actor,
            before.OldName,
            plan.Name,
            wasDefault,
            command.IsDefault,
            EntitlementValueView.RecordsOf(before.OldValues),
            EntitlementValueView.RecordsOf(plan.ValueMap))]);
    }

    private static bool SameValues(IReadOnlyDictionary<string, EntitlementValue> before, IReadOnlyDictionary<string, EntitlementValue> after) =>
        before.Count == after.Count && before.All(value => after.TryGetValue(value.Key, out var other) && other == value.Value);
}
