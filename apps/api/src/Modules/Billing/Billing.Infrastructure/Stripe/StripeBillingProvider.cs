using Billing.Application.Provider;
using Billing.Domain.Subscriptions;
using Microsoft.Extensions.Options;
using Stripe.Checkout;
using Stripe;

namespace Billing.Infrastructure.Stripe;

// Stripe behind IBillingProvider. Our app keeps the plans' prices and writes them to Stripe as Products and Prices: a plan is one Product,
// with the plan's id as its own id, and each amount is one Price, found again by its lookup key. A Price never changes, so a new amount is a
// new Price (Stripe, "Change prices"). The plan a subscription pays for is read back from its Price's Product, whose id Stripe never
// changes. The customer, the Checkout and the subscription carry the tenant and the plan as metadata for support only:
// anyone with access to Stripe's dashboard can edit metadata, so no read takes the tenant from it (B5). Stripe.net retries a failed network call itself, with an idempotency key (Stripe, "Idempotent
// requests"); each call has the time limit of the HTTP client.
internal sealed class StripeBillingProvider(IStripeClient client, IOptions<StripeOptions> settings) : IBillingProvider
{
    private const string TenantKey = "tenant_id";

    private const string PlanKey = "plan_id";

    private const string CheckoutKey = "checkout_key";

    private const string AlreadyExists = "resource_already_exists";

    private const string ProductPrefix = "plan_";

    // The event types whose object names a subscription; any other event is kept and needs no sync.
    private static readonly HashSet<string> SubscriptionEvents =
    [
        EventTypes.CustomerSubscriptionCreated,
        EventTypes.CustomerSubscriptionUpdated,
        EventTypes.CustomerSubscriptionDeleted,
        EventTypes.CustomerSubscriptionPaused,
        EventTypes.CustomerSubscriptionResumed,
    ];

    async Task<string> IBillingProvider.CreateCustomerAsync(Guid tenantId, string? email, CancellationToken cancellationToken)
    {
        var customer = await new CustomerService(client).CreateAsync(
            new CustomerCreateOptions { Email = email, Metadata = new() { [TenantKey] = tenantId.ToString() } },
            new RequestOptions { IdempotencyKey = $"customer-{tenantId}" },
            cancellationToken);
        return customer.Id;
    }

    async Task<CheckoutPage> IBillingProvider.OpenCheckoutAsync(CheckoutOrder order, CancellationToken cancellationToken)
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
            [CheckoutKey] = order.Key.ToString(),
        };
        var session = await new SessionService(client).CreateAsync(
            new SessionCreateOptions
            {
                Mode = "subscription",
                LineItems = lines,
                Customer = order.CustomerId,
                CustomerUpdate = new SessionCustomerUpdateOptions { Address = "auto", Name = "auto" },
                ClientReferenceId = order.TenantId.ToString(),
                BillingAddressCollection = "required",
                TaxIdCollection = new SessionTaxIdCollectionOptions { Enabled = true },
                Metadata = metadata,
                SubscriptionData = new SessionSubscriptionDataOptions { Metadata = metadata },
                SuccessUrl = order.SuccessUrl.ToString(),
                CancelUrl = order.CancelUrl.ToString(),
                ExpiresAt = order.ExpiresAt.UtcDateTime,
            },
            new RequestOptions { IdempotencyKey = $"checkout-{order.Key}" },
            cancellationToken);
        return new CheckoutPage(session.Id, new Uri(session.Url));
    }

    // Every open page of the customer but the one of the kept key, by the key in its metadata, which our app wrote: a page Stripe already
    // closed (paid or expired) is not listed. A race with a payment fails the expiry, and its retry no longer lists the page.
    async Task IBillingProvider.ExpireOpenCheckoutsAsync(string customerId, Guid? keepKey, CancellationToken cancellationToken)
    {
        var sessions = new SessionService(client);
        var keep = keepKey?.ToString();
        await foreach (var session in sessions.ListAutoPagingAsync(new SessionListOptions { Customer = customerId, Status = "open" }, cancellationToken: cancellationToken))
        {
            if (session.Metadata.GetValueOrDefault(CheckoutKey) != keep || keep is null)
            {
                await sessions.ExpireAsync(session.Id, cancellationToken: cancellationToken);
            }
        }
    }

    async Task IBillingProvider.SetCancelAtPeriodEndAsync(string subscriptionId, bool cancelAtPeriodEnd, CancellationToken cancellationToken) =>
        await new SubscriptionService(client).UpdateAsync(
            subscriptionId,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = cancelAtPeriodEnd },
            cancellationToken: cancellationToken);

    async Task IBillingProvider.CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var subscriptions = new SubscriptionService(client);
        if ((await subscriptions.GetAsync(subscriptionId, cancellationToken: cancellationToken)).Status is "canceled" or "incomplete_expired")
        {
            return;
        }

        await subscriptions.CancelAsync(subscriptionId, new SubscriptionCancelOptions { InvoiceNow = false, Prorate = false }, cancellationToken: cancellationToken);
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
        var item = subscription.Items.Data[0];
        if (!item.Price.ProductId.StartsWith(ProductPrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(item.Price.ProductId[ProductPrefix.Length..], "N", out var planId))
        {
            return null;
        }

        return new ProviderSubscription(
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

        var productId = $"{ProductPrefix}{order.PlanId:N}";
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
