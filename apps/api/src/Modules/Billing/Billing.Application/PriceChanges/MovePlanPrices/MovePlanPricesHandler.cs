using Billing.Application.PriceChanges.MoveSubscriptionPrice;
using Wolverine;

namespace Billing.Application.PriceChanges.MovePlanPrices;

// B13: only the plan's latest change moves anybody, as a later edit replaced an earlier one, and a message that arrives before the change's
// day moves nobody: a later "Move to the new price" gave the change another day. Each payer to move is moved in its own tenant, where its
// own state is checked again.
public static class MovePlanPricesHandler
{
    public static async Task<OutgoingMessages> HandleAsync(MovePlanPricesCommand command, IPriceChangeStore changes, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await changes.FindAsync(command.PriceChangeId, cancellationToken) is not { MovesAt: { } movesAt } change
            || movesAt > time.GetUtcNow()
            || (await changes.FindLatestAsync(change.PlanId, cancellationToken))?.Id != change.Id)
        {
            return [];
        }

        var payers = await changes.ListPayersAsync(change.PlanId, cancellationToken);
        OutgoingMessages messages = [];
        messages.AddRange(payers
            .Where(payer => change.Moves(payer.Yearly, payer.BaseAmount, payer.PerPersonAmount))
            .Select(payer => new MoveSubscriptionPriceCommand(change.Id).WithDeliveryOptions(new DeliveryOptions { TenantId = payer.TenantId.ToString() })));
        return messages;
    }
}
