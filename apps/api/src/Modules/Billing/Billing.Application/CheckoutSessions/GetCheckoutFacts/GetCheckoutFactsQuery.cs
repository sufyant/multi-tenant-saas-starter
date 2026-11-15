using SharedKernel;

namespace Billing.Application.CheckoutSessions.GetCheckoutFacts;

/// <summary>
/// What a checkout of the plan needs from ControlPlane, in the tenant of the route: the member who pays, the plan's prices, the plan the
/// tenant is on and how many members take a seat. It reads through ControlPlane's contracts only, so its handler runs on ControlPlane's
/// DbContext and Billing's own reads go in a message of their own (W1).
/// </summary>
[MessageName("get-checkout-facts")]
public sealed record GetCheckoutFactsQuery(Guid TenantId, string ActorId, Guid PlanId);
