using Billing.Domain.PriceChanges;

namespace Billing.Application.PriceChanges.GetPriceChange;

// The payers belong to no tenant (R6), so the read counts across tenants, as the platform's tenant list reads the summaries (R12).
public static class GetPriceChangeHandler
{
    public static async Task<PriceChangeView> HandleAsync(GetPriceChangeQuery query, IPriceChangeStore changes, TimeProvider time, CancellationToken cancellationToken)
    {
        var payers = await changes.ListPayersAsync(query.PlanId, cancellationToken);
        var latest = await changes.FindLatestAsync(query.PlanId, cancellationToken);
        var today = new PlanPrices(query.Prices.BaseMonthly, query.Prices.PerPersonMonthly, query.Prices.BaseYearly, query.Prices.PerPersonYearly);
        var onOld = payers.Count(payer => today.Of(payer.Yearly) != (payer.BaseAmount, payer.PerPersonAmount));
        return new PriceChangeView(
            payers.Count,
            payers.Count(payer => payer.Yearly),
            onOld == 0 ? null : new OldPriceView(onOld, latest is null ? null : ViewOf(latest.OldPrices), latest?.ChangedAt),
            latest?.MovesAt > time.GetUtcNow() ? latest.MovesAt : null);
    }

    internal static PricesView ViewOf(PlanPrices prices) => new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);
}
