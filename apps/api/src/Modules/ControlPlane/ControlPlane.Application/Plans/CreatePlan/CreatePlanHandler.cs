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
        MeterCode meterCode,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var values = EntitlementValueView.Read(command.Entitlements);
        if (!values.IsSuccess)
        {
            return (values.Error, []);
        }

        var meters = MeterSettingView.Read(command.Meters ?? []);
        if (!meters.IsSuccess)
        {
            return (meters.Error, []);
        }

        var now = time.GetUtcNow();
        var nameTaken = await plans.IsNameTakenAsync(command.Name.Trim(), null, cancellationToken);
        var content = new PlanContent(values.Value, command.Prices?.ToPrices() ?? PlanPrices.Zero, meters.Value);
        var created = Plan.Create(Guid.CreateVersion7(now), command.Name, content, meterCode.Catalog, nameTaken, now);
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
        return (
            view,
            [
                new PlanCreatedEvent(
                    Guid.CreateVersion7(now),
                    now,
                    plan.Id,
                    actor,
                    plan.Name,
                    command.IsDefault,
                    EntitlementValueView.RecordsOf(plan.ValueMap),
                    PlanPricesView.RecordOf(plan.Prices),
                    MeterSettingView.RecordsOf(plan.MeterMap)),
            ]);
    }
}
