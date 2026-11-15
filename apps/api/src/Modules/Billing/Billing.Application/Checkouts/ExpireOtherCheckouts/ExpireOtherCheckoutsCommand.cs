using SharedKernel;

namespace Billing.Application.Checkouts.ExpireOtherCheckouts;

/// <summary>
/// Expires every open page of the customer at the provider but the one of the tenant's current checkout, so nobody can pay on a page that
/// no longer is the tenant's checkout (B7). With no key to keep, it expires them all: a payment, a closure, the purge. A page that was paid or
/// expired already is not open, and nothing happens to it.
/// </summary>
/// <param name="KeepKey">The key of the tenant's current checkout, whose page stays; none to expire every open page.</param>
[MessageName("expire-other-checkouts")]
public sealed record ExpireOtherCheckoutsCommand(string CustomerId, Guid? KeepKey);
