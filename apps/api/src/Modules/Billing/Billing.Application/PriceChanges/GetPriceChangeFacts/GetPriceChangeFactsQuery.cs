using SharedKernel;

namespace Billing.Application.PriceChanges.GetPriceChangeFacts;

/// <summary>
/// What the platform's price change endpoints read of ControlPlane, on its DbContext (W1): the plan and its prices of today, none for a plan
/// that does not exist, and our user id of the signed-in system admin (B13).
/// </summary>
/// <param name="ActorId">The identity provider's id of the signed-in system admin (N12).</param>
[MessageName("get-price-change-facts")]
public sealed record GetPriceChangeFactsQuery(string ActorId, Guid PlanId);
