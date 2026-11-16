using Billing.Domain.PriceChanges;

namespace Billing.Application.PriceChanges.GetPriceChange;

// The payers belong to no tenant (R6), so the read counts across tenants, as the platform's tenant list reads the summaries (R12). A payer is
// on an old price when it pays another price than the plan's latest change, as "Move to the new price" moves exactly those (B13); before the
// plan's first change, nobody is.
public static class GetPriceChangeHandler
{
    public static async Task<PriceChangeView> HandleAsync(GetPriceChangeQuery query, IPriceChangeStore changes, TimeProvider time, CancellationToken cancellationToken)
    {
        var payers = await changes.ListPayersAsync(query.PlanId, cancellationToken);
        var latest = await changes.FindLatestAsync(query.PlanId, cancellationToken);
        var onOld = latest is null ? 0 : payers.Count(payer => latest.Moves(payer.Yearly, payer.BaseAmount, payer.PerPersonAmount));
        return new PriceChangeView(
            payers.Count,
            payers.Count(payer => payer.Yearly),
            latest is null || onOld == 0 ? null : new OldPriceView(onOld, ViewOf(latest.OldPrices), latest.ChangedAt),
            latest?.MovesAt > time.GetUtcNow() ? latest.MovesAt : null);
    }

    internal static PricesView ViewOf(PlanPrices prices) => new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);
}
