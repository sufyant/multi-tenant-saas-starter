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

    private const string CardError = "card_error";

    // The part of a Price's lookup key that names the fee per person (B2).
    private const string PerPerson = "_per_person_";

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
            lines.Add(new() { Price = await PriceAsync(order.PlanId, order.PlanName, order.Currency, order.Yearly, "base", order.BaseAmount, cancellationToken), Quantity = 1 });
        }

        if (order.PerPersonAmount > 0 && order.People > 0)
        {
            lines.Add(new() { Price = await PriceAsync(order.PlanId, order.PlanName, order.Currency, order.Yearly, "per_person", order.PerPersonAmount, cancellationToken), Quantity = order.People });
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

    // The open pages of the customer that the key made, by the key in their metadata, which our app wrote: a page Stripe already closed (paid
    // or expired) is not listed. A race with a payment fails the expiry, and its retry no longer lists the page.
    async Task IBillingProvider.ExpireOpenCheckoutsAsync(string customerId, Guid key, CancellationToken cancellationToken)
    {
        var sessions = new SessionService(client);
        await foreach (var session in sessions.ListAutoPagingAsync(new SessionListOptions { Customer = customerId, Status = "open" }, cancellationToken: cancellationToken))
        {
            if (session.Metadata.GetValueOrDefault(CheckoutKey) == key.ToString())
            {
                await sessions.ExpireAsync(session.Id, cancellationToken: cancellationToken);
            }
        }
    }

    // Stripe refuses an end set on a subscription that a schedule manages, so a scheduled move is released first: an end replaces it (B9).
    async Task IBillingProvider.SetCancelAtPeriodEndAsync(string subscriptionId, bool cancelAtPeriodEnd, CancellationToken cancellationToken)
    {
        var subscriptions = new SubscriptionService(client);
        await ReleaseScheduleAsync(await subscriptions.GetAsync(subscriptionId, cancellationToken: cancellationToken), cancellationToken);
        await subscriptions.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions { CancelAtPeriodEnd = cancelAtPeriodEnd }, cancellationToken: cancellationToken);
    }

    async Task<PlanChangePreview> IBillingProvider.PreviewPlanChangeAsync(PlanChangeOrder order, DateTimeOffset proratedAt, CancellationToken cancellationToken)
    {
        var subscription = await new SubscriptionService(client).GetAsync(order.SubscriptionId, cancellationToken: cancellationToken);
        var lines = await LinesAsync(order, cancellationToken);
        var invoice = await new InvoiceService(client).CreatePreviewAsync(
            new InvoiceCreatePreviewOptions
            {
                Customer = subscription.CustomerId,
                Subscription = subscription.Id,
                SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
                {
                    Items =
                    [
                        .. subscription.Items.Data.Select(item => new InvoiceSubscriptionDetailsItemOptions { Id = item.Id, Deleted = true }),
                        .. lines.Select(line => new InvoiceSubscriptionDetailsItemOptions { Price = line.Price, Quantity = line.Quantity }),
                    ],
                    ProrationBehavior = "always_invoice",
                    ProrationDate = proratedAt.UtcDateTime,
                },
            },
            cancellationToken: cancellationToken);
        return new PlanChangePreview(invoice.TotalExcludingTax ?? invoice.Total, proratedAt);
    }

    // Stripe, "Update a subscription", payment_behavior error_if_incomplete: a declined card answers a card error and Stripe keeps the
    // subscription as it was, with no open invoice that a retry or the customer could pay later. A move from monthly to yearly starts a new
    // period today, which Stripe does when the interval changes. Only after the payment does the move drop a scheduled move and a member's
    // end, so a declined card keeps both.
    async Task<bool> IBillingProvider.ChangePlanNowAsync(PlanChangeOrder order, DateTimeOffset proratedAt, CancellationToken cancellationToken)
    {
        var subscriptions = new SubscriptionService(client);
        var subscription = await subscriptions.GetAsync(order.SubscriptionId, cancellationToken: cancellationToken);
        var lines = await LinesAsync(order, cancellationToken);
        try
        {
            await subscriptions.UpdateAsync(
                subscription.Id,
                new SubscriptionUpdateOptions
                {
                    Items =
                    [
                        .. subscription.Items.Data.Select(item => new SubscriptionItemOptions { Id = item.Id, Deleted = true }),
                        .. lines.Select(line => new SubscriptionItemOptions { Price = line.Price, Quantity = line.Quantity }),
                    ],
                    ProrationBehavior = "always_invoice",
                    ProrationDate = proratedAt.UtcDateTime,
                    PaymentBehavior = "error_if_incomplete",
                },
                cancellationToken: cancellationToken);
        }
        catch (StripeException exception) when (exception.StripeError?.Type == CardError)
        {
            return false;
        }

        await ReleaseScheduleAsync(subscription, cancellationToken);
        if (subscription.CancelAtPeriodEnd)
        {
            await subscriptions.UpdateAsync(subscription.Id, new SubscriptionUpdateOptions { CancelAtPeriodEnd = false }, cancellationToken: cancellationToken);
        }

        return true;
    }

    // Stripe, "Subscription schedules", "Downgrade at the end of the period": the schedule keeps today's phase until the period's end, then
    // one period of the order, and releases the subscription, which goes on with the order. A scheduled move before is replaced. A member's
    // end becomes the schedule's end behavior when Stripe makes the schedule from the subscription, and the update's release replaces it,
    // in the same call that writes the move, so a failed call leaves the end as it was.
    async Task IBillingProvider.SchedulePlanChangeAsync(PlanChangeOrder order, CancellationToken cancellationToken)
    {
        var subscription = await new SubscriptionService(client).GetAsync(order.SubscriptionId, cancellationToken: cancellationToken);
        var schedules = new SubscriptionScheduleService(client);
        var schedule = subscription.ScheduleId is { } scheduleId
            ? await schedules.GetAsync(scheduleId, cancellationToken: cancellationToken)
            : await schedules.CreateAsync(new SubscriptionScheduleCreateOptions { FromSubscription = subscription.Id }, cancellationToken: cancellationToken);
        var lines = await LinesAsync(order, cancellationToken);
        await schedules.UpdateAsync(
            schedule.Id,
            new SubscriptionScheduleUpdateOptions
            {
                EndBehavior = "release",
                ProrationBehavior = "none",
                Phases =
                [
                    new SubscriptionSchedulePhaseOptions
                    {
                        StartDate = schedule.CurrentPhase.StartDate,
                        EndDate = subscription.Items.Data[0].CurrentPeriodEnd,
                        Items = [.. subscription.Items.Data.Select(item => new SubscriptionSchedulePhaseItemOptions { Price = item.Price.Id, Quantity = item.Quantity })],
                    },
                    new SubscriptionSchedulePhaseOptions
                    {
                        Items = [.. lines.Select(line => new SubscriptionSchedulePhaseItemOptions { Price = line.Price, Quantity = line.Quantity })],
                        Duration = new SubscriptionSchedulePhaseDurationOptions { Interval = order.Yearly ? "year" : "month", IntervalCount = 1 },
                    },
                ],
            },
            cancellationToken: cancellationToken);
    }

    async Task IBillingProvider.UndoScheduledChangeAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var subscriptions = new SubscriptionService(client);
        var subscription = await subscriptions.GetAsync(subscriptionId, cancellationToken: cancellationToken);
        await ReleaseScheduleAsync(subscription, cancellationToken);
        if (subscription.CancelAtPeriodEnd)
        {
            await subscriptions.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions { CancelAtPeriodEnd = false }, cancellationToken: cancellationToken);
        }
    }

    async Task IBillingProvider.CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var subscriptions = new SubscriptionService(client);
        if ((await subscriptions.GetAsync(subscriptionId, cancellationToken: cancellationToken)).Status is "canceled" or "incomplete_expired")
        {
            return;
        }

        await subscriptions.CancelAsync(subscriptionId, new SubscriptionCancelOptions { InvoiceNow = false, Prorate = false }, cancellationToken: cancellationToken);
    }

    // Step 40, billing per person: only the quantity of the fee per person changes, at its own Price. A join on a yearly subscription is
    // invoiced at once; a removal, and any change of a monthly one, is prorated on the next invoice (Stripe, "Prorations"). A subscription
    // that a schedule manages takes the count in the schedule's phases from the current one on, so the move at the period's end keeps it;
    // Stripe applies the current phase's change to the subscription with the same proration.
    async Task IBillingProvider.SetSeatsAsync(string subscriptionId, int people, CancellationToken cancellationToken)
    {
        var subscriptions = new SubscriptionService(client);
        var subscription = await subscriptions.GetAsync(subscriptionId, cancellationToken: cancellationToken);
        if (subscription.Status is "canceled" or "incomplete_expired"
            || subscription.Items.Data.FirstOrDefault(item => IsPerPerson(item.Price)) is not { } seats || seats.Quantity == people)
        {
            return;
        }

        var proration = seats.Price.Recurring.Interval == "year" && people > seats.Quantity ? "always_invoice" : "create_prorations";
        if (subscription.ScheduleId is not { } scheduleId)
        {
            await subscriptions.UpdateAsync(
                subscriptionId,
                new SubscriptionUpdateOptions { Items = [new SubscriptionItemOptions { Id = seats.Id, Quantity = people }], ProrationBehavior = proration },
                cancellationToken: cancellationToken);
            return;
        }

        var schedules = new SubscriptionScheduleService(client);
        var schedule = await schedules.GetAsync(scheduleId, new SubscriptionScheduleGetOptions { Expand = ["phases.items.price"] }, cancellationToken: cancellationToken);
        await schedules.UpdateAsync(
            scheduleId,
            new SubscriptionScheduleUpdateOptions
            {
                ProrationBehavior = proration,
                Phases =
                [
                    .. schedule.Phases.Where(phase => phase.StartDate >= schedule.CurrentPhase.StartDate).Select(phase => new SubscriptionSchedulePhaseOptions
                    {
                        StartDate = phase.StartDate,
                        EndDate = phase.EndDate,
                        Items =
                        [
                            .. phase.Items.Select(item => new SubscriptionSchedulePhaseItemOptions
                            {
                                Price = item.Price.Id,
                                Quantity = IsPerPerson(item.Price) ? people : item.Quantity,
                            }),
                        ],
                    }),
                ],
            },
            cancellationToken: cancellationToken);
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
        var subscription = await new SubscriptionService(client).GetAsync(
            subscriptionId, new SubscriptionGetOptions { Expand = ["schedule.phases.items.price"] }, cancellationToken: cancellationToken);
        var item = subscription.Items.Data[0];
        if (PlanOf(item.Price) is not { } planId)
        {
            return null;
        }

        // The phase after the paid period is the move a member scheduled (B9); a schedule that ended or was released schedules nothing.
        var next = subscription.Schedule is { Status: "active" or "not_started" } schedule
            ? schedule.Phases.FirstOrDefault(phase => phase.StartDate >= item.CurrentPeriodEnd)?.Items.FirstOrDefault()?.Price
            : null;
        var scheduledPlanId = next is null ? null : PlanOf(next);

        return new ProviderSubscription(
            subscription.CustomerId,
            subscription.Id,
            planId,
            item.Price.Recurring.Interval == "year",
            StatusOf(subscription.Status).ToString(),
            item.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd,
            scheduledPlanId,
            scheduledPlanId is null ? null : next!.Recurring.Interval == "year",
            subscription.Items.Data.FirstOrDefault(seats => IsPerPerson(seats.Price)) is { } perPerson ? (int)perPerson.Quantity : null);
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

    // The fee per person is the Price whose lookup key our app wrote with its part (B2).
    private static bool IsPerPerson(Price price) => price.LookupKey?.Contains(PerPerson, StringComparison.Ordinal) == true;

    // The plan of a Price is its Product's id, which our app wrote (B2); a Product of no plan of ours names none.
    private static Guid? PlanOf(Price price) =>
        price.ProductId.StartsWith(ProductPrefix, StringComparison.Ordinal) && Guid.TryParseExact(price.ProductId[ProductPrefix.Length..], "N", out var planId)
            ? planId
            : null;

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
    private async Task<string> PriceAsync(Guid planId, string planName, string planCurrency, bool yearly, string part, long amount, CancellationToken cancellationToken)
    {
        var interval = yearly ? "year" : "month";
        var currency = planCurrency.ToLowerInvariant();
        var lookupKey = $"plan_{planId:N}_{part}_{interval}_{currency}_{amount}";
        var prices = new PriceService(client);
        if (await FindPriceAsync(prices, lookupKey, cancellationToken) is { } found)
        {
            return found;
        }

        var productId = $"{ProductPrefix}{planId:N}";
        try
        {
            await new ProductService(client).CreateAsync(new ProductCreateOptions { Id = productId, Name = planName }, cancellationToken: cancellationToken);
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
                    Nickname = $"{planName}, {part.Replace('_', ' ')}, {interval}ly",
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

    // The order's Prices: the base fee once and the fee per person for each seated member; a price of 0 is left out, as in Checkout (B3).
    private async Task<List<(string Price, long Quantity)>> LinesAsync(PlanChangeOrder order, CancellationToken cancellationToken)
    {
        List<(string Price, long Quantity)> lines = [];
        if (order.BaseAmount > 0)
        {
            lines.Add((await PriceAsync(order.PlanId, order.PlanName, order.Currency, order.Yearly, "base", order.BaseAmount, cancellationToken), 1));
        }

        if (order.PerPersonAmount > 0 && order.People > 0)
        {
            lines.Add((await PriceAsync(order.PlanId, order.PlanName, order.Currency, order.Yearly, "per_person", order.PerPersonAmount, cancellationToken), order.People));
        }

        return lines;
    }

    private async Task ReleaseScheduleAsync(global::Stripe.Subscription subscription, CancellationToken cancellationToken)
    {
        if (subscription.ScheduleId is { } scheduleId)
        {
            await new SubscriptionScheduleService(client).ReleaseAsync(scheduleId, cancellationToken: cancellationToken);
        }
    }
}
