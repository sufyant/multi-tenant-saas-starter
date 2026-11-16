namespace Billing.Application.PriceChanges.GetPriceChange;

/// <summary>A plan's four prices, each a whole number of the minor unit of the deployment's currency.</summary>
public sealed record PricesView(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly);
