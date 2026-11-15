using System.Collections.Concurrent;
using System.Text.Json;
using Billing.Application.Provider;

namespace Api.IntegrationTests.Fixtures;

// Stripe is a system we do not own, so the tests fake it behind IBillingProvider (section 9). A checkout and a portal answer a link of the
// fake and keep what they were asked; a subscription is what the test put there. A webhook's body is the fake's own JSON, {"id", "type",
// "subscriptionId"}, and only the signature Signature proves it: the real signature is the adapter's, which Billing.UnitTests checks.
internal sealed class FakeBillingProvider : IBillingProvider
{
    public const string Signature = "t=1,v1=fake";

    private readonly ConcurrentDictionary<string, ProviderSubscription> _subscriptions = new();

    public static Uri CheckoutLink { get; } = new("https://checkout.stripe.test/c/pay/cs_test_1");

    public static Uri PortalLink { get; } = new("https://billing.stripe.test/p/session/bps_test_1");

    public ConcurrentQueue<CheckoutOrder> Checkouts { get; } = [];

    public ConcurrentQueue<(string CustomerId, Uri ReturnUrl)> Portals { get; } = [];

    public ConcurrentQueue<string> Reads { get; } = [];

    // A webhook's body of the fake.
    public static string Body(string eventId, string type, string? subscriptionId) =>
        JsonSerializer.Serialize(new { id = eventId, type, subscriptionId }, JsonSerializerOptions.Web);

    public void Put(ProviderSubscription subscription) => _subscriptions[subscription.SubscriptionId] = subscription;

    public Task<Uri> OpenCheckoutAsync(CheckoutOrder order, CancellationToken cancellationToken)
    {
        Checkouts.Enqueue(order);
        return Task.FromResult(CheckoutLink);
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
