using SharedKernel;

namespace Billing.Application.PriceChanges.AnnounceTenantPriceChange;

/// <summary>Tells the tenant of the transaction of the price change, when it still pays another price for the plan (B13).</summary>
[MessageName("announce-tenant-price-change")]
public sealed record AnnounceTenantPriceChangeCommand(Guid PriceChangeId);
