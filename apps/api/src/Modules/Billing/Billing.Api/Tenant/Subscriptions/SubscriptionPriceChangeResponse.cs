namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// A price change that the tenant was told of (10.18): from its first period after <c>movesAt</c> it pays the plan's new prices; the
/// plan's prices before and after (B13).
/// </summary>
public sealed record SubscriptionPriceChangeResponse(DateTimeOffset MovesAt, PricesResponse OldPrices, PricesResponse NewPrices);
