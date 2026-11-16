using Billing.Application.Contracts;
using Billing.Application.PriceChanges.NamePriceChange;
using Billing.Application.PriceChanges.RecordPriceChange;
using Wolverine;

namespace Billing.Application.PriceChanges.AnnounceTenantPriceChange;

// B13: in the tenant's transaction (W3). A Contract tenant is billed outside the app (B12), and a payer that ends, left the plan or pays the
// new price already is told nothing. The tenant's name is read on ControlPlane's DbContext, by the next message (W1); the event's id is
// chosen here, so a copy of that message publishes the same event (I1).
public static class AnnounceTenantPriceChangeHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        AnnounceTenantPriceChangeCommand command,
        IPriceChangeStore changes,
        IContractRepository contracts,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await changes.FindAsync(command.PriceChangeId, cancellationToken) is not { MovesAt: { } movesAt } change
            || await contracts.IsContractAsync(cancellationToken)
            || await changes.FindPayerAsync(cancellationToken) is not { Ends: false } payer
            || payer.PlanId != change.PlanId
            || !change.Moves(payer.Yearly, payer.BaseAmount, payer.PerPersonAmount))
        {
            return [];
        }

        var now = time.GetUtcNow();
        return
        [
            new NamePriceChangeCommand(
                Guid.CreateVersion7(now), now, payer.TenantId, change.PlanId, change.PlanName, change.Currency, payer.Yearly,
                RecordPriceChangeHandler.RecordOf(change.OldPrices), RecordPriceChangeHandler.RecordOf(change.NewPrices), movesAt),
        ];
    }
}
