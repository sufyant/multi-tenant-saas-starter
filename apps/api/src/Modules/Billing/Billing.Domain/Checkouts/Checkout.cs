using SharedKernel;

namespace Billing.Domain.Checkouts;

/// <summary>
/// A tenant's one open checkout (B7): the plan, the interval and the seats it pays for, the key that the provider's idempotency keeps it
/// by, and its page once the provider made it. Two members who ask for the same checkout at once get the same key, so the provider makes
/// one page; a checkout of another order replaces it, and the replaced page is expired at the provider. So a tenant has at most one page
/// that can start a subscription.
/// </summary>
internal sealed class Checkout : ITenantEntity
{
    /// <summary>How long before its expiry a page is not handed out again: a person needs time to pay.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(10);

    private Checkout(Guid key, Guid planId, bool yearly, int people, DateTimeOffset createdAt)
    {
        Key = key;
        PlanId = planId;
        Yearly = yearly;
        People = people;
        CreatedAt = createdAt;
    }

    /// <summary>The provider's idempotency key of the page.</summary>
    public Guid Key { get; private set; }

    public Guid PlanId { get; private set; }

    public bool Yearly { get; private set; }

    /// <summary>The seats the page charges the fee per person for.</summary>
    public int People { get; private set; }

    /// <summary>The provider's id of the page; none while the provider is making it.</summary>
    public string? SessionId { get; private set; }

    public string? Url { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>A tenant's first checkout.</summary>
    public static Checkout Open(Guid key, Guid planId, bool yearly, int people, DateTimeOffset at) => new(key, planId, yearly, people, at);

    /// <summary>
    /// The checkout of this order, at this moment: this one when it is the same order and its page is being made or still has time,
    /// else a new one under a new key. Answers the page that the new one replaces, which the provider must expire, when there is one.
    /// </summary>
    public (StateChange Change, string? Replaced) Reserve(Guid newKey, Guid planId, bool yearly, int people, DateTimeOffset at)
    {
        var same = PlanId == planId && Yearly == yearly && People == people;
        var usable = Url is null ? at - CreatedAt < Margin : ExpiresAt - Margin > at;
        if (same && usable)
        {
            return (StateChange.Unchanged, null);
        }

        var replaced = Url is not null && ExpiresAt > at ? SessionId : null;
        (Key, PlanId, Yearly, People, SessionId, Url, ExpiresAt, CreatedAt) = (newKey, planId, yearly, people, null, null, null, at);
        return (StateChange.Changed, replaced);
    }

    /// <summary>Keeps the provider's page of the key; a page of a key that another checkout replaced since changes nothing.</summary>
    public StateChange Record(Guid key, string sessionId, string url, DateTimeOffset expiresAt)
    {
        if (key != Key || SessionId == sessionId)
        {
            return StateChange.Unchanged;
        }

        (SessionId, Url, ExpiresAt) = (sessionId, url, expiresAt);
        return StateChange.Changed;
    }
}
