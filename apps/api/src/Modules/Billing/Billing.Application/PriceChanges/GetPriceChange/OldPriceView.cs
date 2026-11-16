namespace Billing.Application.PriceChanges.GetPriceChange;

/// <summary>
/// The payers on another price than the plan's latest change (6.15): how many, the prices before that change and since when new customers
/// pay the new ones.
/// </summary>
public sealed record OldPriceView(int Count, PricesView OldPrices, DateTimeOffset Since);
