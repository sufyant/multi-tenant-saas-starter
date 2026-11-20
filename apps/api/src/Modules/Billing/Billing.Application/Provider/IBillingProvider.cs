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

    /// <summary>Sends the customer's invoices and receipts to this address from now on (B14; Stripe, "Customer emails").</summary>
    Task SetCustomerEmailAsync(string customerId, string email, CancellationToken cancellationToken);

    /// <summary>
    /// A page of the provider where the person pays for the order by card, until the order's expiry. The provider
    /// writes the plan's prices as its own prices when it has none of these amounts yet, as a price never changes there (Stripe, "Change
    /// prices"). The order names the tenant's customer, the checkout's idempotency key and its expiry, so every call of one key is the same call
    /// and makes one page (B7), and the checkout's key goes into the page's metadata, which the expiry of the key's pages reads; the
    /// signed event of the payment then names the subscription.
    /// </summary>
    Task<CheckoutPage> OpenCheckoutAsync(CheckoutOrder order, CancellationToken cancellationToken);

    /// <summary>
    /// Expires the open checkout pages of the customer that this key made, by the key in each page's metadata, so nobody can pay on them
    /// (B7). A page that was paid or expired already is not open.
    /// </summary>
    Task ExpireOpenCheckoutsAsync(string customerId, Guid key, CancellationToken cancellationToken);

    /// <summary>What the move at once costs today, prorated for the rest of the period, without tax (B9).</summary>
    Task<PlanChangePreview> PreviewPlanChangeAsync(PlanChangeOrder order, DateTimeOffset proratedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the subscription to the order at once, with proration, and charges the prorated amount now (B9). A scheduled move or end is
    /// dropped. False when the card was declined: the subscription stays as it was (Stripe, "Pending updates").
    /// </summary>
    Task<bool> ChangePlanNowAsync(PlanChangeOrder order, DateTimeOffset proratedAt, CancellationToken cancellationToken);

    /// <summary>Moves the subscription to the order at the end of the period it has paid for, in place of any move or end scheduled before (B9).</summary>
    Task SchedulePlanChangeAsync(PlanChangeOrder order, CancellationToken cancellationToken);

    /// <summary>Drops the move or the end scheduled at the end of the period, so the subscription goes on as it is (B9).</summary>
    Task UndoScheduledChangeAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>Ends the subscription at the end of the period it has paid for, or keeps it (B8). A scheduled move is dropped.</summary>
    Task SetCancelAtPeriodEndAsync(string subscriptionId, bool cancelAtPeriodEnd, CancellationToken cancellationToken);

    /// <summary>Ends the subscription at once, with no refund and no new invoice; one that has ended already changes nothing (B8).</summary>
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the subscription's fee per person to this many seated members (step 40, billing per person): a join on a yearly subscription
    /// is charged at once, and any other change is prorated on the next invoice (Stripe, "Prorations"). A move scheduled at the period's end
    /// takes the count too. A subscription without a fee per person, one that has ended, or one with this count already changes nothing.
    /// </summary>
    Task SetSeatsAsync(string subscriptionId, int people, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the subscription to the Prices of the order's amounts, with the same quantities and no proration, so it pays them from its next
    /// period (B13). A part of 0 is left out, and a part the subscription had none of is added once for the base fee. The order's people
    /// are not read.
    /// </summary>
    Task MovePricesAsync(PlanChangeOrder order, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the subscription at once and refunds every payment of its latest invoice in full (B7): a second subscription that a race let
    /// through paid for nothing. One that has ended already is only refunded; each refund has its payment's idempotency key, so a retry
    /// refunds nothing twice.
    /// </summary>
    Task EndAndRefundAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a meter's extra of a month to the subscription's next invoice, as one line of its amount (E11). The tenant, the meter and the
    /// month are the call's idempotency key, so two calls within the provider's window add one line (Stripe, "Idempotent requests").
    /// </summary>
    Task AddExtraAsync(ExtraCharge charge, CancellationToken cancellationToken);

    /// <summary>The provider's page where the customer manages its card, address, VAT ID and invoices, which works for a short time.</summary>
    Task<Uri> OpenPortalAsync(string customerId, Uri returnUrl, CancellationToken cancellationToken);

    /// <summary>
    /// The customer's page in the provider's dashboard, for a system admin (4.27): it needs the admin's own sign-in there, so it is no secret.
    /// It reads no network.
    /// </summary>
    Uri CustomerPageOf(string customerId);

    /// <summary>The subscription as the provider holds it now; none when it pays for no plan of ours.</summary>
    Task<ProviderSubscription?> FindSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// The event in a webhook's body, when its signature is the provider's for this body and recent enough; else none (Stripe, "Webhooks",
    /// "Verify webhook signatures"). It reads no network.
    /// </summary>
    ProviderEvent? ReadEvent(string body, string? signature);
}
