using Billing.Application.Provider;
using SharedKernel;

namespace Billing.Application.PriceChanges.MovePrices;

/// <summary>
/// Asks the provider to move the subscription to the Prices of the order, with no proration, so it pays them from its next period (B13).
/// The sync that follows brings the new amounts to the tenant's payer.
/// </summary>
[MessageName("move-prices")]
public sealed record MovePricesCommand(PlanChangeOrder Order);
