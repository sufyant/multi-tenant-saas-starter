using SharedKernel;

namespace Billing.Application.Checkouts.ReserveCheckout;

/// <summary>
/// The tenant's one open checkout of this order (B7), in the tenant of the route: the open one when it is the same order and its page still
/// has time, else a new one, and every other open page of the customer is expired.
/// </summary>
[MessageName("reserve-checkout")]
public sealed record ReserveCheckoutCommand(Guid PlanId, bool Yearly, int People, Uri ReturnUrl);
