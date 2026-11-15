using SharedKernel;

namespace Billing.Application.Checkouts.AbandonCheckout;

/// <summary>
/// Gives up the tenant's checkout of this key after the provider's call failed, in the tenant of the route (B7): the provider keeps a failed
/// result under its key too, so the next request takes a new key. Pages the key may have made are expired.
/// </summary>
[MessageName("abandon-checkout")]
public sealed record AbandonCheckoutCommand(Guid Key);
