using Billing.Api.Tenant.Subscriptions;

namespace Billing.Api.Platform.PriceChanges;

/// <summary>
/// The payers on a price the plan no longer has (6.15): how many, the plan's prices before its latest change, and since when new customers
/// pay the new ones; both null for payers of a price older than billing knows of.
/// </summary>
public sealed record OldPriceResponse(int Count, PricesResponse? OldPrices, DateTimeOffset? Since);
