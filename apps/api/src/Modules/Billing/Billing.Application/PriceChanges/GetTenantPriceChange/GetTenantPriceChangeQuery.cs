using SharedKernel;

namespace Billing.Application.PriceChanges.GetTenantPriceChange;

/// <summary>The price change that the tenant of the transaction was told of and that has not applied yet (B13, 10.18); none when nothing is due.</summary>
[MessageName("get-tenant-price-change")]
public sealed record GetTenantPriceChangeQuery;
