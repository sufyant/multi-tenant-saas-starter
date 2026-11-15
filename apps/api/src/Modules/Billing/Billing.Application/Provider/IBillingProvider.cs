namespace Billing.Application.Provider;

/// <summary>
/// The billing provider, Stripe, behind one interface (step 40): it keeps the money, the prices, the subscription, the card, the address,
/// the VAT ID and the invoices; our app keeps the rules. Every call has a time limit (Nygard, <i>Release It!</i>, chapter 5), and no
/// transaction is open during a call (section 3, "Handlers and pipeline"). The tests use a fake of their own (section 9).
/// </summary>
/// <remarks>Public because a public handler takes it (W9), and a test's fake implements it. Its records speak no domain type: a status crosses
/// as its name (N7r).</remarks>
public interface IBillingProvider
{
    /// <summary>
    /// Creates the tenant's customer, with the person's address for its receipts. The tenant's id is the call's idempotency key, so two calls
    /// within the provider's window get one customer (Stripe, "Idempotent requests"). The tenant's id goes into the customer's metadata for
    /// support, never to be read back (B5).
    /// </summary>
    Task<string> CreateCustomerAsync(Guid tenantId, string? email, CancellationToken cancellationToken);

    /// <summary>
    /// A page of the provider where the person pays for the order by card, until the order's expiry. The provider
    /// writes the plan's prices as its own prices when it has none of these amounts yet, as a price never changes there (Stripe, "Change
    /// prices"). The order names the tenant's customer, the checkout's idempotency key and its expiry, so every call of one key is the same call
    /// and makes one page (B7), and the checkout's key goes into the page's metadata, which the expiry of the customer's other pages reads; the
    /// signed event of the payment then names the subscription.
    /// </summary>
    Task<CheckoutPage> OpenCheckoutAsync(CheckoutOrder order, CancellationToken cancellationToken);

    /// <summary>
    /// Expires every open checkout page of the customer but the one of this key, or all of them without a key, so nobody can pay on them
    /// (B7). A page that was paid or expired already is not open.
    /// </summary>
    Task ExpireOpenCheckoutsAsync(string customerId, Guid? keepKey, CancellationToken cancellationToken);

    /// <summary>Ends the subscription at the end of the period it has paid for, or keeps it (B8).</summary>
    Task SetCancelAtPeriodEndAsync(string subscriptionId, bool cancelAtPeriodEnd, CancellationToken cancellationToken);

    /// <summary>Ends the subscription at once, with no refund and no new invoice; one that has ended already changes nothing (B8).</summary>
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>The provider's page where the customer manages its card, address, VAT ID and invoices, which works for a short time.</summary>
    Task<Uri> OpenPortalAsync(string customerId, Uri returnUrl, CancellationToken cancellationToken);

    /// <summary>The subscription as the provider holds it now; none when it pays for no plan of ours.</summary>
    Task<ProviderSubscription?> FindSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// The event in a webhook's body, when its signature is the provider's for this body and recent enough; else none (Stripe, "Webhooks",
    /// "Verify webhook signatures"). It reads no network.
    /// </summary>
    ProviderEvent? ReadEvent(string body, string? signature);
}
