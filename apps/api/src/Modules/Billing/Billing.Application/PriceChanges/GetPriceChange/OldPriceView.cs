namespace Billing.Application.PriceChanges.GetPriceChange;

/// <summary>
/// The payers on a price the plan no longer has (6.15): how many, the prices before the plan's latest change and since when new customers
/// pay the new ones; the prices and the moment are none for payers of an older price than billing knows of.
/// </summary>
public sealed record OldPriceView(int Count, PricesView? OldPrices, DateTimeOffset? Since);
