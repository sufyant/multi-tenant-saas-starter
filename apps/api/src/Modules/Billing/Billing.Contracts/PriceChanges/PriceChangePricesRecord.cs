namespace Billing.Contracts.PriceChanges;

/// <summary>A plan's four prices in a price change's event, each a whole number of the minor unit of its currency (B13).</summary>
public sealed record PriceChangePricesRecord(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly);
