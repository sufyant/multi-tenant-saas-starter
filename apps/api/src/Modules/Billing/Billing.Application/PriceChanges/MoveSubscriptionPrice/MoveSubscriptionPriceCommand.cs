using SharedKernel;

namespace Billing.Application.PriceChanges.MoveSubscriptionPrice;

/// <summary>Moves the subscription of the tenant of the transaction to the new prices of the change, when it still pays another price (B13).</summary>
[MessageName("move-subscription-price")]
public sealed record MoveSubscriptionPriceCommand(Guid PriceChangeId);
