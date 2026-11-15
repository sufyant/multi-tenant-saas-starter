using SharedKernel;

namespace Billing.Application.Checkouts.ExpireCheckoutPages;

/// <summary>
/// Expires the open pages of the customer that a checkout key made, once the key no longer is the tenant's checkout: another order replaced
/// it, a payment, a closure or the purge ended it (B7). It names the key, so a late delivery never expires the page of a newer checkout. A
/// page that was paid or expired already is not open, and nothing happens to it.
/// </summary>
[MessageName("expire-checkout-pages")]
public sealed record ExpireCheckoutPagesCommand(string CustomerId, Guid Key);
