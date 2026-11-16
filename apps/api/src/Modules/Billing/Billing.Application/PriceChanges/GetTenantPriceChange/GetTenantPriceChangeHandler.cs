using Billing.Application.PriceChanges.GetPriceChange;

namespace Billing.Application.PriceChanges.GetTenantPriceChange;

// The tenant's payer names its plan; the plan's latest change shows from its notice until its day, while the tenant pays another price and
// its subscription does not end at its period's end (B13). A Contract tenant's payer ends (B12).
public static class GetTenantPriceChangeHandler
{
    public static async Task<TenantPriceChangeView?> HandleAsync(GetTenantPriceChangeQuery query, IPriceChangeStore changes, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (await changes.FindPayerAsync(cancellationToken) is not { Ends: false } payer
            || await changes.FindLatestAsync(payer.PlanId, cancellationToken) is not { MovesAt: { } movesAt, AnnouncesAt: { } announcesAt } change
            || movesAt <= now
            || announcesAt > now
            || !change.Moves(payer.Yearly, payer.BaseAmount, payer.PerPersonAmount))
        {
            return null;
        }

        return new TenantPriceChangeView(movesAt, GetPriceChangeHandler.ViewOf(change.OldPrices), GetPriceChangeHandler.ViewOf(change.NewPrices));
    }
}
