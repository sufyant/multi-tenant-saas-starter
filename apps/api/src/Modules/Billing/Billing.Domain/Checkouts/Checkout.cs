using SharedKernel;

namespace Billing.Domain.Checkouts;

/// <summary>
/// A tenant's one open checkout (B7): the order it pays for (the plan, the interval, the seats and the page to come back to), the key that
/// the provider's idempotency keeps its page by, the moment the page stops working, and the page once the provider made it. The same order
/// keeps the same key while the page has time, so every request of it sends the provider the same call and gets the same page; another
/// order takes a new key, and the provider expires every other open page of the customer.
/// </summary>
internal sealed class Checkout : ITenantEntity
{
    /// <summary>How long a page works: the shortest window that leaves a person time to pay; Stripe allows 30 minutes to 24 hours.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>How long before its expiry a page is not handed out again: a person needs time to pay.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(10);

    // For EF Core, which cannot bind the order.
    private Checkout()
    {
        ReturnUrl = string.Empty;
    }

    private Checkout(Guid key, CheckoutOrderOf order, DateTimeOffset at)
    {
        Key = key;
        (PlanId, Yearly, People, ReturnUrl) = order;
        ExpiresAt = at + Lifetime;
    }

    /// <summary>The provider's idempotency key of the page.</summary>
    public Guid Key { get; private set; }

    public Guid PlanId { get; private set; }

    public bool Yearly { get; private set; }

    /// <summary>The seats the page charges the fee per person for.</summary>
    public int People { get; private set; }

    /// <summary>The web app's page the provider sends the person back to.</summary>
    public string ReturnUrl { get; private set; }

    /// <summary>When the page stops working, fixed at the reservation, so every call of the key asks the provider the same.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>The provider's id of the page; none while the provider is making it.</summary>
    public string? SessionId { get; private set; }

    public string? Url { get; private set; }

    /// <summary>A tenant's first checkout.</summary>
    public static Checkout Open(Guid key, CheckoutOrderOf order, DateTimeOffset at) => new(key, order, at);

    /// <summary>
    /// The checkout of this order, at this moment: this one when it is the same order and its page has time, else a new one under a new
    /// key, whose replaced page the provider must expire with every other open page of the customer.
    /// </summary>
    public StateChange Reserve(Guid newKey, CheckoutOrderOf order, DateTimeOffset at)
    {
        if (order == new CheckoutOrderOf(PlanId, Yearly, People, ReturnUrl) && ExpiresAt - Margin > at)
        {
            return StateChange.Unchanged;
        }

        Key = newKey;
        (PlanId, Yearly, People, ReturnUrl) = order;
        (ExpiresAt, SessionId, Url) = (at + Lifetime, null, null);
        return StateChange.Changed;
    }

    /// <summary>
    /// Keeps the provider's page of the key. False for a page of a key that another checkout replaced since: that page must not stay
    /// payable.
    /// </summary>
    public bool Record(Guid key, string sessionId, string url)
    {
        if (key != Key)
        {
            return false;
        }

        (SessionId, Url) = (sessionId, url);
        return true;
    }
}
