namespace Billing.Application.PriceChanges.GetPriceChangeFacts;

/// <summary>The plan's id, and our user id of the system admin who acts.</summary>
public sealed record PriceChangeFacts(Guid PlanId, Guid ActorId);
