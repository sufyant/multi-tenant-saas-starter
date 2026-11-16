using Billing.Application.PriceChanges.AnnounceTenantPriceChange;
using Wolverine;

namespace Billing.Application.PriceChanges.AnnouncePriceChange;

// B13: only the plan's latest change tells anybody, as a later edit replaced an earlier one. The payers whose subscription does not end at
// its period's end and who pay another price than the new one are told, each in its own tenant, where its own state is checked again.
public static class AnnouncePriceChangeHandler
{
    public static async Task<OutgoingMessages> HandleAsync(AnnouncePriceChangeCommand command, IPriceChangeStore changes, CancellationToken cancellationToken)
    {
        if (await changes.FindAsync(command.PriceChangeId, cancellationToken) is not { } change
            || (await changes.FindLatestAsync(change.PlanId, cancellationToken))?.Id != change.Id)
        {
            return [];
        }

        var payers = await changes.ListPayersAsync(change.PlanId, cancellationToken);
        OutgoingMessages messages = [];
        messages.AddRange(payers
            .Where(payer => change.Moves(payer.Yearly, payer.BaseAmount, payer.PerPersonAmount))
            .Select(payer => new AnnounceTenantPriceChangeCommand(change.Id).WithDeliveryOptions(new DeliveryOptions { TenantId = payer.TenantId.ToString() })));
        return messages;
    }
}
