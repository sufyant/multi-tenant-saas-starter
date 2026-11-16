using Billing.Api.Tenant.Subscriptions;

namespace Billing.Api.Platform.PriceChanges;

/// <summary>
/// The payers on another price than the plan's latest change (6.15): how many, the plan's prices before that change, and since when new
/// customers pay the new ones.
/// </summary>
public sealed record OldPriceResponse(int Count, PricesResponse OldPrices, DateTimeOffset Since);
