using System.Collections.Concurrent;
using System.Text.Json;
using Billing.Application.Provider;

namespace Api.IntegrationTests.Fixtures;

// Stripe is a system we do not own, so the tests fake it behind IBillingProvider (section 9). A customer is cus_ and the tenant's id. A
// checkout's page is one for each idempotency key, as Stripe keeps a key (cs_ and the key), and a portal answers a link of the fake; each
// keeps what it was asked; an expiry, an end at the period's end and an end at once are kept too; a subscription is what the test put there. A webhook's body is the fake's own JSON, {"id", "type",
// "subscriptionId"}, and only the signature Signature proves it: the real signature is the adapter's, which Billing.UnitTests checks.
internal sealed class FakeBillingProvider : IBillingProvider
{
    public const string Signature = "t=1,v1=fake";

    private readonly ConcurrentDictionary<string, ProviderSubscription> _subscriptions = new();

    private readonly ConcurrentDictionary<Guid, CheckoutPage> _pages = new();

    private Func<Task>? _whileOpening;

    public static Uri PortalLink { get; } = new("https://billing.stripe.test/p/session/bps_test_1");

    public ConcurrentQueue<Guid> Customers { get; } = [];

    public ConcurrentQueue<CheckoutOrder> Checkouts { get; } = [];

    // Runs once, while the next checkout's page is being made, as another request that comes between.
    public Func<Task>? WhileOpeningCheckout { get => _whileOpening; set => _whileOpening = value; }

    public ConcurrentQueue<(string CustomerId, Uri ReturnUrl)> Portals { get; } = [];

    public ConcurrentQueue<string> Reads { get; } = [];

    // Each expiry of the open pages that a checkout key made.
    public ConcurrentQueue<(string CustomerId, Guid Key)> Expiries { get; } = [];

    public ConcurrentQueue<(string SubscriptionId, bool CancelAtPeriodEnd)> PeriodEnds { get; } = [];

    public ConcurrentQueue<string> Cancelled { get; } = [];

    public static string CustomerOf(Guid tenantId) => $"cus_{tenantId:N}";

    // The page of a checkout's key, the same one for each call of the key.
    public static Uri PageOf(Guid key) => new($"https://checkout.stripe.test/c/pay/cs_{key:N}");

    // A webhook's body of the fake.
    public static string Body(string eventId, string type, string? subscriptionId) =>
        JsonSerializer.Serialize(new { id = eventId, type, subscriptionId }, JsonSerializerOptions.Web);

    public void Put(ProviderSubscription subscription) => _subscriptions[subscription.SubscriptionId] = subscription;

    public Task<string> CreateCustomerAsync(Guid tenantId, string? email, CancellationToken cancellationToken)
    {
        Customers.Enqueue(tenantId);
        return Task.FromResult(CustomerOf(tenantId));
    }

    public async Task<CheckoutPage> OpenCheckoutAsync(CheckoutOrder order, CancellationToken cancellationToken)
    {
        Checkouts.Enqueue(order);
        if (Interlocked.Exchange(ref _whileOpening, null) is { } between)
        {
            await between();
        }

        return _pages.GetOrAdd(order.Key, key => new CheckoutPage($"cs_{key:N}", PageOf(key)));
    }

    public Task ExpireOpenCheckoutsAsync(string customerId, Guid key, CancellationToken cancellationToken)
    {
        Expiries.Enqueue((customerId, key));
        return Task.CompletedTask;
    }

    public Task SetCancelAtPeriodEndAsync(string subscriptionId, bool cancelAtPeriodEnd, CancellationToken cancellationToken)
    {
        PeriodEnds.Enqueue((subscriptionId, cancelAtPeriodEnd));
        return Task.CompletedTask;
    }

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        Cancelled.Enqueue(subscriptionId);
        return Task.CompletedTask;
    }

    public Task<Uri> OpenPortalAsync(string customerId, Uri returnUrl, CancellationToken cancellationToken)
    {
        Portals.Enqueue((customerId, returnUrl));
        return Task.FromResult(PortalLink);
    }

    public Task<ProviderSubscription?> FindSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        Reads.Enqueue(subscriptionId);
        return Task.FromResult(_subscriptions.GetValueOrDefault(subscriptionId));
    }

    public ProviderEvent? ReadEvent(string body, string? signature)
    {
        if (signature != Signature)
        {
            return null;
        }

        var read = JsonSerializer.Deserialize<FakeEvent>(body, JsonSerializerOptions.Web)!;
        return new ProviderEvent(read.Id, read.Type, read.SubscriptionId);
    }

    private sealed record FakeEvent(string Id, string Type, string? SubscriptionId);
}
