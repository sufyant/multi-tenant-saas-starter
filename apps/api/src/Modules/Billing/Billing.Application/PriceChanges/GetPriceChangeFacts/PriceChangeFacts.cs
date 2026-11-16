using Billing.Application.PriceChanges.GetPriceChange;

namespace Billing.Application.PriceChanges.GetPriceChangeFacts;

/// <summary>The plan with its prices of today, and our user id of the system admin who acts.</summary>
public sealed record PriceChangeFacts(Guid PlanId, PricesView Prices, Guid ActorId);
