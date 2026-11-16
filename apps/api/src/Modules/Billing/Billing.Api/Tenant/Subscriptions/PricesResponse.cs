using Billing.Application.PriceChanges.GetPriceChange;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>A plan's four prices in a price change, each a whole number of the minor unit of the deployment's currency, without tax.</summary>
public sealed record PricesResponse(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly)
{
    internal static PricesResponse Of(PricesView prices) => new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);
}
