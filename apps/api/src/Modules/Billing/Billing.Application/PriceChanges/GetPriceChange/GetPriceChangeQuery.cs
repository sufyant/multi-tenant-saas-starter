using SharedKernel;

namespace Billing.Application.PriceChanges.GetPriceChange;

/// <summary>The payers of the plan and its latest price change (B13, 6.14, 6.15).</summary>
[MessageName("get-price-change")]
public sealed record GetPriceChangeQuery(Guid PlanId);
