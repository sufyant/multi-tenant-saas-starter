using SharedKernel;

namespace Billing.Application.Checkouts.ExpireCheckoutSession;

/// <summary>
/// Expires a page of the provider that no longer is the tenant's checkout, so nobody can pay on it (B7). A page that was paid or expired
/// already changes nothing.
/// </summary>
[MessageName("expire-checkout-session")]
public sealed record ExpireCheckoutSessionCommand(string SessionId);
