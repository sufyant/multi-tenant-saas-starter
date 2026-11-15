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
    /// A page of the provider where the person pays for the order by card, which works for a short time and is never stored. The provider
    /// writes the plan's prices as its own prices when it has none of these amounts yet, as a price never changes there (Stripe, "Change
    /// prices"). It creates the customer at the first payment, and the signed event of the payment then names the subscription.
    /// </summary>
    Task<Uri> OpenCheckoutAsync(CheckoutOrder order, CancellationToken cancellationToken);

    /// <summary>The provider's page where the customer manages its card, address, VAT ID and invoices, which works for a short time.</summary>
    Task<Uri> OpenPortalAsync(string customerId, Uri returnUrl, CancellationToken cancellationToken);

    /// <summary>The subscription as the provider holds it now; none when it is not one of ours, as it names no tenant.</summary>
    Task<ProviderSubscription?> FindSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// The event in a webhook's body, when its signature is the provider's for this body and recent enough; else none (Stripe, "Webhooks",
    /// "Verify webhook signatures"). It reads no network.
    /// </summary>
    ProviderEvent? ReadEvent(string body, string? signature);
}
