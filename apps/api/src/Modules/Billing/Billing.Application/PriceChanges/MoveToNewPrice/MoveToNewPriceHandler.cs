using Billing.Application.PriceChanges.RecordPriceChange;
using Billing.Contracts.PriceChanges;
using Billing.Domain.PriceChanges;
using SharedKernel;
using Wolverine;

namespace Billing.Application.PriceChanges.MoveToNewPrice;

// B13: the plan's latest change is locked, so two moves of one plan run one after the other. A move scheduled already keeps its day and
// schedules nothing more (I1). Nobody on another price is 409 billing.nothing_to_move, before any change (W7).
public static class MoveToNewPriceHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(MoveToNewPriceCommand command, IPriceChangeStore changes, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await changes.FindLatestForUpdateAsync(command.PlanId, cancellationToken) is not { } change)
        {
            return (Errors.NothingToMove, []);
        }

        var payers = await changes.ListPayersAsync(command.PlanId, cancellationToken);
        if (!payers.Any(payer => change.Moves(payer.Yearly, payer.BaseAmount, payer.PerPersonAmount)))
        {
            return (Errors.NothingToMove, []);
        }

        var now = time.GetUtcNow();
        if (!change.MoveAfterNotice(now))
        {
            return (Result.Success(), []);
        }

        await changes.SaveChangesAsync(cancellationToken);
        return (Result.Success(),
        [
            new PriceChangeScheduledEvent(
                Guid.CreateVersion7(now), now, change.PlanId, change.PlanName, command.ActorId, change.Currency,
                RecordPriceChangeHandler.RecordOf(change.OldPrices), RecordPriceChangeHandler.RecordOf(change.NewPrices), now + PriceChange.Notice),
            .. RecordPriceChangeHandler.Scheduled(change, now),
        ]);
    }
}
