using System.Collections.Concurrent;
using System.Text.Json;
using Billing.Application.Provider;

namespace Api.IntegrationTests.Fixtures;

// Stripe is a system we do not own, so the tests fake it behind IBillingProvider (section 9). A customer is cus_ and the tenant's id. A
// checkout's page is one for each idempotency key, as Stripe keeps a key (cs_ and the key), and a portal answers a link of the fake; each
// keeps what it was asked; an expiry, an end at the period's end, an end at once, and each move of a plan and its undo are kept too; a subscription is what the test put there. A webhook's body is the fake's own JSON, {"id", "type",
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

    // Each end at once with a refund of what the subscription paid (B7).
    public ConcurrentQueue<string> Refunded { get; } = [];

    // Each move at once with its proration moment, each move at the period's end, and each undo (B9).
    public ConcurrentQueue<(PlanChangeOrder Order, DateTimeOffset ProratedAt)> MovesNow { get; } = [];

    public ConcurrentQueue<PlanChangeOrder> Scheduled { get; } = [];

    public ConcurrentQueue<string> Undone { get; } = [];

    // Each count of seated members that the fee per person was set to (step 40, billing per person).
    public ConcurrentQueue<(string SubscriptionId, int People)> Seats { get; } = [];

    // Each move of a subscription to the Prices of a plan's new amounts, its base fee and its fee per person (B13).
    public ConcurrentQueue<(string SubscriptionId, long BaseAmount, long PerPersonAmount)> PriceMoves { get; } = [];

    // The prorated amount of a preview, and whether the card pays a move at once.
    public long PreviewAmount { get; set; } = 5325;

    public bool Declines { get; set; }

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

    // As Stripe, an end drops a scheduled move.
    public Task SetCancelAtPeriodEndAsync(string subscriptionId, bool cancelAtPeriodEnd, CancellationToken cancellationToken)
    {
        PeriodEnds.Enqueue((subscriptionId, cancelAtPeriodEnd));
        Change(subscriptionId, held => held with { CancelAtPeriodEnd = cancelAtPeriodEnd, ScheduledPlanId = null, ScheduledYearly = null });
        return Task.CompletedTask;
    }

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        Cancelled.Enqueue(subscriptionId);
        return Task.CompletedTask;
    }

    public Task EndAndRefundAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        Refunded.Enqueue(subscriptionId);
        return Task.CompletedTask;
    }

    public Task<PlanChangePreview> PreviewPlanChangeAsync(PlanChangeOrder order, DateTimeOffset proratedAt, CancellationToken cancellationToken) =>
        Task.FromResult(new PlanChangePreview(PreviewAmount, proratedAt));

    // As Stripe, the subscription the fake holds takes each move, so the sync that follows reads it.
    public Task<bool> ChangePlanNowAsync(PlanChangeOrder order, DateTimeOffset proratedAt, CancellationToken cancellationToken)
    {
        MovesNow.Enqueue((order, proratedAt));
        if (Declines)
        {
            return Task.FromResult(false);
        }

        Change(order.SubscriptionId, held => held with
        {
            PlanId = order.PlanId,
            Yearly = order.Yearly,
            CancelAtPeriodEnd = false,
            ScheduledPlanId = null,
            ScheduledYearly = null,
        });
        return Task.FromResult(true);
    }

    public Task SchedulePlanChangeAsync(PlanChangeOrder order, CancellationToken cancellationToken)
    {
        Scheduled.Enqueue(order);
        Change(order.SubscriptionId, held => held with { CancelAtPeriodEnd = false, ScheduledPlanId = order.PlanId, ScheduledYearly = order.Yearly });
        return Task.CompletedTask;
    }

    public Task UndoScheduledChangeAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        Undone.Enqueue(subscriptionId);
        Change(subscriptionId, held => held with { CancelAtPeriodEnd = false, ScheduledPlanId = null, ScheduledYearly = null });
        return Task.CompletedTask;
    }

    // As Stripe, the subscription the fake holds takes the count, so the sync that follows reads it.
    public Task SetSeatsAsync(string subscriptionId, int people, CancellationToken cancellationToken)
    {
        Seats.Enqueue((subscriptionId, people));
        Change(subscriptionId, held => held with { People = people });
        return Task.CompletedTask;
    }

    // As Stripe, the subscription the fake holds takes the new amounts, so the sync that follows reads them.
    public Task MovePricesAsync(PlanChangeOrder order, CancellationToken cancellationToken)
    {
        PriceMoves.Enqueue((order.SubscriptionId, order.BaseAmount, order.PerPersonAmount));
        Change(order.SubscriptionId, held => held with { BaseAmount = order.BaseAmount, PerPersonAmount = order.PerPersonAmount });
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

    private void Change(string subscriptionId, Func<ProviderSubscription, ProviderSubscription> change)
    {
        if (_subscriptions.TryGetValue(subscriptionId, out var held))
        {
            _subscriptions[subscriptionId] = change(held);
        }
    }

    private sealed record FakeEvent(string Id, string Type, string? SubscriptionId);
}
