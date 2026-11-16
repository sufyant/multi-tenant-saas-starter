using SharedKernel;

namespace Billing.Application.PriceChanges.MovePlanPrices;

/// <summary>
/// Moves each tenant that pays another price for the plan of the change to the new one, on the change's day (B13). It travels without a
/// tenant, as a plan belongs to none, and sends one message for each tenant to move, in that tenant (W3).
/// </summary>
[MessageName("move-plan-prices")]
public sealed record MovePlanPricesCommand(Guid PriceChangeId);
