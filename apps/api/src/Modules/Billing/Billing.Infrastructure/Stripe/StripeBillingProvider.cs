using Billing.Application.Provider;
using Billing.Domain.Subscriptions;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Billing.Infrastructure.Stripe;

// Stripe behind IBillingProvider. Our app keeps the plans' prices and writes them to Stripe as Products and Prices: a plan is one Product,
// with the plan's id as its own id, and each amount is one Price, found again by its lookup key. A Price never changes, so a new amount is a
// new Price (Stripe, "Change prices"). The subscription and the Checkout carry the tenant, the plan and the member who paid as metadata,
// which every later read takes them from. Stripe.net retries a failed network call itself, with an idempotency key (Stripe, "Idempotent
// requests"); each call has the time limit of the HTTP client.
internal sealed class StripeBillingProvider(IStripeClient client, IOptions<StripeOptions> settings) : IBillingProvider
{
    public const string TenantKey = "tenant_id";

    public const string PlanKey = "plan_id";

    public const string PaidByKey = "paid_by";

    private const string AlreadyExists = "resource_already_exists";

    // The event types whose object names a subscription; any other event is kept and needs no sync.
    private static readonly HashSet<string> SubscriptionEvents =
    [
        EventTypes.CustomerSubscriptionCreated,
        EventTypes.CustomerSubscriptionUpdated,
        EventTypes.CustomerSubscriptionDeleted,
        EventTypes.CustomerSubscriptionPaused,
        EventTypes.CustomerSubscriptionResumed,
    ];

    async Task<Uri> IBillingProvider.OpenCheckoutAsync(CheckoutOrder order, CancellationToken cancellationToken)
    {
        var lines = new List<SessionLineItemOptions>();
        if (order.BaseAmount > 0)
        {
            lines.Add(new() { Price = await PriceAsync(order, "base", order.BaseAmount, cancellationToken), Quantity = 1 });
        }

        if (order.PerPersonAmount > 0 && order.People > 0)
        {
            lines.Add(new() { Price = await PriceAsync(order, "per_person", order.PerPersonAmount, cancellationToken), Quantity = order.People });
        }

        Dictionary<string, string> metadata = new()
        {
            [TenantKey] = order.TenantId.ToString(),
            [PlanKey] = order.PlanId.ToString(),
            [PaidByKey] = order.PaidBy.ToString(),
        };
        var session = await new SessionService(client).CreateAsync(
            new SessionCreateOptions
            {
                Mode = "subscription",
                LineItems = lines,
                Customer = order.CustomerId,
                CustomerEmail = order.CustomerId is null ? order.Email : null,
                CustomerUpdate = order.CustomerId is null ? null : new SessionCustomerUpdateOptions { Address = "auto", Name = "auto" },
                ClientReferenceId = order.TenantId.ToString(),
                BillingAddressCollection = "required",
                TaxIdCollection = new SessionTaxIdCollectionOptions { Enabled = true },
                Metadata = metadata,
                SubscriptionData = new SessionSubscriptionDataOptions { Metadata = metadata },
                SuccessUrl = order.SuccessUrl.ToString(),
                CancelUrl = order.CancelUrl.ToString(),
            },
            cancellationToken: cancellationToken);
        return new Uri(session.Url);
    }

    async Task<Uri> IBillingProvider.OpenPortalAsync(string customerId, Uri returnUrl, CancellationToken cancellationToken)
    {
        var session = await new global::Stripe.BillingPortal.SessionService(client).CreateAsync(
            new global::Stripe.BillingPortal.SessionCreateOptions { Customer = customerId, ReturnUrl = returnUrl.ToString() },
            cancellationToken: cancellationToken);
        return new Uri(session.Url);
    }

    async Task<ProviderSubscription?> IBillingProvider.FindSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await new SubscriptionService(client).GetAsync(subscriptionId, cancellationToken: cancellationToken);
        if (!Guid.TryParse(subscription.Metadata.GetValueOrDefault(TenantKey), out var tenantId)
            || !Guid.TryParse(subscription.Metadata.GetValueOrDefault(PlanKey), out var planId)
            || !Guid.TryParse(subscription.Metadata.GetValueOrDefault(PaidByKey), out var paidBy))
        {
            return null;
        }

        var item = subscription.Items.Data[0];
        return new ProviderSubscription(
            tenantId,
            paidBy,
            subscription.CustomerId,
            subscription.Id,
            planId,
            item.Price.Recurring.Interval == "year",
            StatusOf(subscription.Status).ToString(),
            item.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd);
    }

    ProviderEvent? IBillingProvider.ReadEvent(string body, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return null;
        }

        Event read;
        try
        {
            read = EventUtility.ConstructEvent(
                body, signature, settings.Value.WebhookSecret, (long)settings.Value.WebhookTolerance.TotalSeconds, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return null;
        }

        var subscriptionId = read.Data.Object switch
        {
            Session session when read.Type == EventTypes.CheckoutSessionCompleted => session.SubscriptionId,
            global::Stripe.Subscription subscription when SubscriptionEvents.Contains(read.Type) => subscription.Id,
            _ => null,
        };
        return new ProviderEvent(read.Id, read.Type, subscriptionId);
    }

    private static SubscriptionStatus StatusOf(string status) => status switch
    {
        "incomplete" => SubscriptionStatus.Incomplete,
        "incomplete_expired" => SubscriptionStatus.IncompleteExpired,
        "trialing" => SubscriptionStatus.Trialing,
        "active" => SubscriptionStatus.Active,
        "past_due" => SubscriptionStatus.PastDue,
        "canceled" => SubscriptionStatus.Canceled,
        "unpaid" => SubscriptionStatus.Unpaid,
        "paused" => SubscriptionStatus.Paused,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Stripe answered a subscription status the code does not know."),
    };

    private static async Task<string?> FindPriceAsync(PriceService prices, string lookupKey, CancellationToken cancellationToken) =>
        (await prices.ListAsync(new PriceListOptions { LookupKeys = [lookupKey], Active = true }, cancellationToken: cancellationToken)).Data.FirstOrDefault()?.Id;

    // The plan's Product, with the plan's id as its id, then the Price of this amount, found by its lookup key or created with it. Two
    // checkouts at once may both create it: the lookup key is unique in the account, so the second's create fails and it reads the first's.
    private async Task<string> PriceAsync(CheckoutOrder order, string part, long amount, CancellationToken cancellationToken)
    {
        var interval = order.Yearly ? "year" : "month";
        var currency = order.Currency.ToLowerInvariant();
        var lookupKey = $"plan_{order.PlanId:N}_{part}_{interval}_{currency}_{amount}";
        var prices = new PriceService(client);
        if (await FindPriceAsync(prices, lookupKey, cancellationToken) is { } found)
        {
            return found;
        }

        var productId = $"plan_{order.PlanId:N}";
        try
        {
            await new ProductService(client).CreateAsync(new ProductCreateOptions { Id = productId, Name = order.PlanName }, cancellationToken: cancellationToken);
        }
        catch (StripeException exception) when (exception.StripeError?.Code == AlreadyExists)
        {
            // The plan's Product exists from an earlier price.
        }

        try
        {
            var price = await prices.CreateAsync(
                new PriceCreateOptions
                {
                    Product = productId,
                    Currency = currency,
                    UnitAmount = amount,
                    Recurring = new PriceRecurringOptions { Interval = interval },
                    LookupKey = lookupKey,
                    Nickname = $"{order.PlanName}, {part.Replace('_', ' ')}, {interval}ly",
                },
                cancellationToken: cancellationToken);
            return price.Id;
        }
        catch (StripeException)
        {
            if (await FindPriceAsync(prices, lookupKey, cancellationToken) is { } raced)
            {
                return raced;
            }

            throw;
        }
    }
}
