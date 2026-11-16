using Billing.Application.PriceChanges.GetPriceChange;

namespace Billing.Application.PriceChanges.GetTenantPriceChange;

/// <summary>The day the tenant's subscription moves to the plan's new prices, and the plan's prices before and after (10.18).</summary>
public sealed record TenantPriceChangeView(DateTimeOffset MovesAt, PricesView OldPrices, PricesView NewPrices);
