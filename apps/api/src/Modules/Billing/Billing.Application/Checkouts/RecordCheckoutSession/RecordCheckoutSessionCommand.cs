using SharedKernel;

namespace Billing.Application.Checkouts.RecordCheckoutSession;

/// <summary>
/// Keeps the provider's page of the tenant's checkout of this key, in the tenant of the route (B7). Answers false for a page of a key that
/// another checkout, a payment or a closure replaced since: that page is expired, and the person is not sent to it.
/// </summary>
[MessageName("record-checkout-session")]
public sealed record RecordCheckoutSessionCommand(Guid Key, string SessionId, Uri Url);
