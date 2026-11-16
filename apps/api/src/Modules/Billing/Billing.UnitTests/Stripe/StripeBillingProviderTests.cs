using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Billing.Application.Provider;
using Billing.Infrastructure.Stripe;
using Microsoft.Extensions.Options;
using Stripe;

namespace Billing.UnitTests.Stripe;

// The webhook's signature (A5's exception, step 40): only a body that Stripe signed with the endpoint's secret, recently enough, is read
// (Stripe, "Webhooks", "Verify webhook signatures"). The signature is built here as Stripe builds it: HMAC-SHA256 of "{timestamp}.{body}".
// No call leaves the process: ReadEvent reads no network.
public sealed class StripeBillingProviderTests
{
    private const string Secret = "whsec_test_secret";

    private static readonly IBillingProvider Provider = new StripeBillingProvider(
        new StripeClient("sk_test_unused"),
        Options.Create(new StripeOptions { SecretKey = "sk_test_unused", WebhookSecret = Secret }));

    private static readonly Guid Team = Guid.Parse("0198a0d2-0000-7000-8000-000000000001");

    private static readonly Guid Business = Guid.Parse("0198a0d2-0000-7000-8000-000000000002");

    private static readonly PlanChangeOrder ToBusiness = new("sub_1", Business, "Business", "EUR", false, 9900, 800, 10);

    private static readonly Func<StripeRequests, IBillingProvider> ProviderOf = stripe =>
        new StripeBillingProvider(
            new StripeClient("sk_test_unused", httpClient: new SystemNetHttpClient(new HttpClient(stripe), maxNetworkRetries: 0)),
            Options.Create(new StripeOptions { SecretKey = "sk_test_unused", WebhookSecret = "whsec_unused" }));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void ReadEvent_ACompletedCheckoutSignedByStripe_NamesItsSubscription()
    {
        var body = EventBody("evt_1", "checkout.session.completed", """{"id": "cs_1", "object": "checkout.session", "subscription": "sub_1"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, Secret, DateTimeOffset.UtcNow));

        read.ShouldBe(new ProviderEvent("evt_1", "checkout.session.completed", "sub_1"));
    }

    [Fact]
    public void ReadEvent_AnUpdatedSubscription_NamesTheSubscription()
    {
        var body = EventBody("evt_2", "customer.subscription.updated", """{"id": "sub_2", "object": "subscription"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, Secret, DateTimeOffset.UtcNow));

        read.ShouldBe(new ProviderEvent("evt_2", "customer.subscription.updated", "sub_2"));
    }

    // An event about no subscription is kept, and asks for no sync.
    [Fact]
    public void ReadEvent_AnInvoiceEvent_NamesNoSubscription()
    {
        var body = EventBody("evt_3", "invoice.paid", """{"id": "in_3", "object": "invoice"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, Secret, DateTimeOffset.UtcNow));

        read.ShouldBe(new ProviderEvent("evt_3", "invoice.paid", null));
    }

    [Fact]
    public void ReadEvent_SignedWithAnotherSecret_ReadsNothing()
    {
        var body = EventBody("evt_4", "customer.subscription.updated", """{"id": "sub_4", "object": "subscription"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, "whsec_someone_else", DateTimeOffset.UtcNow));

        read.ShouldBeNull();
    }

    [Fact]
    public void ReadEvent_ABodyChangedAfterItsSignature_ReadsNothing()
    {
        var body = EventBody("evt_5", "customer.subscription.updated", """{"id": "sub_5", "object": "subscription"}""");
        var signature = SignatureOf(body, Secret, DateTimeOffset.UtcNow);

        var read = Provider.ReadEvent(body.Replace("sub_5", "sub_other", StringComparison.Ordinal), signature);

        read.ShouldBeNull();
    }

    // Against a replay: a signature older than the tolerance of 5 minutes is refused.
    [Fact]
    public void ReadEvent_ASignatureOlderThanTheTolerance_ReadsNothing()
    {
        var body = EventBody("evt_6", "customer.subscription.updated", """{"id": "sub_6", "object": "subscription"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, Secret, DateTimeOffset.UtcNow.AddMinutes(-6)));

        read.ShouldBeNull();
    }

    [Fact]
    public void ReadEvent_WithoutASignature_ReadsNothing()
    {
        var body = EventBody("evt_7", "customer.subscription.updated", """{"id": "sub_7", "object": "subscription"}""");

        var read = Provider.ReadEvent(body, null);

        read.ShouldBeNull();
    }

    // B9: the plan changes against Stripe's API, answered in the process: a declined card changes nothing, a paid move drops a scheduled
    // move only after the payment, a move at the period's end writes the schedule's two phases, and the read names the next phase's move.

    [Fact]
    public async Task ChangePlanNow_ADeclinedCard_AnswersFalseAndKeepsTheSchedule()
    {
        var stripe = Subscribed().Answer("POST", "/v1/subscriptions/sub_1", """{"error": {"type": "card_error", "code": "card_declined", "message": "declined"}}""", HttpStatusCode.PaymentRequired);

        var paid = await ProviderOf(stripe).ChangePlanNowAsync(ToBusiness, DateTimeOffset.UnixEpoch, Cancellation);

        paid.ShouldBeFalse();
        stripe.Sent.ShouldNotContain(request => request.Path.Contains("/release", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangePlanNow_APaidMove_ChargesNowThenReleasesTheSchedule()
    {
        var stripe = Subscribed()
            .Answer("POST", "/v1/subscriptions/sub_1", SubscriptionJson(scheduleId: "sub_sched_1"))
            .Answer("POST", "/v1/subscription_schedules/sub_sched_1/release", """{"id": "sub_sched_1", "object": "subscription_schedule"}""");

        var paid = await ProviderOf(stripe).ChangePlanNowAsync(ToBusiness, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), Cancellation);

        paid.ShouldBeTrue();
        var posts = stripe.Sent.Where(request => request.Method == "POST").Select(request => request.Path).ToList();
        posts.ShouldBe(["/v1/subscriptions/sub_1", "/v1/subscription_schedules/sub_sched_1/release"]);
        var update = stripe.Sent.Single(request => request.Method == "POST" && request.Path == "/v1/subscriptions/sub_1").Body;
        update.ShouldContain("payment_behavior=error_if_incomplete");
        update.ShouldContain("proration_behavior=always_invoice");
        update.ShouldContain("proration_date=1800000000");
        update.Split('&').ShouldContain("items[0][id]=si_1");
        update.Split('&').ShouldContain("items[0][deleted]=true");
    }

    [Fact]
    public async Task SchedulePlanChange_WithoutASchedule_KeepsTodayUntilThePeriodsEndThenOnePeriodOfTheOrder()
    {
        var stripe = Subscribed(scheduleId: null)
            .Answer("POST", "/v1/subscription_schedules/sub_sched_new", """{"id": "sub_sched_new", "object": "subscription_schedule"}""")
            .Answer("POST", "/v1/subscription_schedules", """{"id": "sub_sched_new", "object": "subscription_schedule", "current_phase": {"start_date": 1790000000, "end_date": 1800000000}}""");

        await ProviderOf(stripe).SchedulePlanChangeAsync(ToBusiness with { PlanId = Team, PlanName = "Team", BaseAmount = 0, PerPersonAmount = 1000 }, Cancellation);

        var update = stripe.Sent.Single(request => request.Path == "/v1/subscription_schedules/sub_sched_new").Body;
        update.Split('&').ShouldBe(
            [
                "end_behavior=release", "phases[0][end_date]=1800000000", "phases[0][items][0][price]=price_team_old", "phases[0][items][0][quantity]=10",
                "phases[0][start_date]=1790000000", "phases[1][duration][interval]=month", "phases[1][duration][interval_count]=1",
                "phases[1][items][0][price]=price_found", "phases[1][items][0][quantity]=10", "proration_behavior=none",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task FindSubscription_AScheduledMove_NamesThePlanAndIntervalOfTheNextPhase()
    {
        var stripe = new StripeRequests().Answer("GET", "/v1/subscriptions/sub_1", SubscriptionJson(scheduleId: null, schedule: $$$"""
            {"id": "sub_sched_1", "object": "subscription_schedule", "status": "active", "phases": [
              {"start_date": 1790000000, "end_date": 1800000000, "items": [{"price": {"id": "price_b", "object": "price", "product": "plan_{{{Business:N}}}", "recurring": {"interval": "month"} }, "quantity": 10}]},
              {"start_date": 1800000000, "end_date": 1802592000, "items": [{"price": {"id": "price_t", "object": "price", "product": "plan_{{{Team:N}}}", "recurring": {"interval": "year"} }, "quantity": 10}]}
            ]}
            """));

        var found = await ProviderOf(stripe).FindSubscriptionAsync("sub_1", Cancellation);

        (found!.PlanId, found.ScheduledPlanId, found.ScheduledYearly).ShouldBe((Business, Team, true));
        stripe.Sent.Single().Path.ShouldContain("expand[0]=schedule.phases.items.price");
    }

    // Step 40, billing per person: the fee per person follows the seated members. A join on a monthly subscription, and any removal, is
    // prorated on the next invoice; a join on a yearly subscription is charged at once; the same count sends nothing; a scheduled move
    // takes the count in both phases; and the read names the count Stripe holds.

    [Fact]
    public async Task SetSeats_AJoinOnAMonthlySubscription_ProratesItOnTheNextInvoice()
    {
        var stripe = Seated("month", 10).Answer("POST", "/v1/subscriptions/sub_1", SeatedJson("month", 11));

        await ProviderOf(stripe).SetSeatsAsync("sub_1", 11, Cancellation);

        stripe.Sent.Single(request => request.Method == "POST").Body.Split('&').ShouldBe(
            ["items[0][id]=si_seats", "items[0][quantity]=11", "proration_behavior=create_prorations"], ignoreOrder: true);
    }

    [Fact]
    public async Task SetSeats_AJoinOnAYearlySubscription_ChargesItAtOnce()
    {
        var stripe = Seated("year", 10).Answer("POST", "/v1/subscriptions/sub_1", SeatedJson("year", 11));

        await ProviderOf(stripe).SetSeatsAsync("sub_1", 11, Cancellation);

        stripe.Sent.Single(request => request.Method == "POST").Body.Split('&').ShouldContain("proration_behavior=always_invoice");
    }

    [Fact]
    public async Task SetSeats_ARemovalOnAYearlySubscription_ProratesItOnTheNextInvoice()
    {
        var stripe = Seated("year", 10).Answer("POST", "/v1/subscriptions/sub_1", SeatedJson("year", 9));

        await ProviderOf(stripe).SetSeatsAsync("sub_1", 9, Cancellation);

        stripe.Sent.Single(request => request.Method == "POST").Body.Split('&').ShouldContain("proration_behavior=create_prorations");
    }

    [Fact]
    public async Task SetSeats_TheCountStripeHolds_SendsNothing()
    {
        var stripe = Seated("month", 10);

        await ProviderOf(stripe).SetSeatsAsync("sub_1", 10, Cancellation);

        stripe.Sent.ShouldNotContain(request => request.Method == "POST");
    }

    [Fact]
    public async Task SetSeats_AScheduledMove_SetsTheCountInBothPhases()
    {
        var stripe = new StripeRequests()
            .Answer("GET", "/v1/subscriptions/sub_1", SeatedJson("month", 10, "\"sub_sched_1\""))
            .Answer("GET", "/v1/subscription_schedules/sub_sched_1", $$$"""
                {"id": "sub_sched_1", "object": "subscription_schedule", "status": "active", "current_phase": {"start_date": 1790000000, "end_date": 1800000000}, "phases": [
                  {"start_date": 1790000000, "end_date": 1800000000, "items": [
                    {"price": {"id": "price_base", "object": "price", "lookup_key": "plan_b_base_month_eur_9900"}, "quantity": 1},
                    {"price": {"id": "price_seats", "object": "price", "lookup_key": "plan_b_per_person_month_eur_800"}, "quantity": 10}]},
                  {"start_date": 1800000000, "end_date": 1802592000, "items": [
                    {"price": {"id": "price_team", "object": "price", "lookup_key": "plan_t_per_person_month_eur_1000"}, "quantity": 10}]}
                ]}
                """)
            .Answer("POST", "/v1/subscription_schedules/sub_sched_1", """{"id": "sub_sched_1", "object": "subscription_schedule"}""");

        await ProviderOf(stripe).SetSeatsAsync("sub_1", 11, Cancellation);

        stripe.Sent.Single(request => request.Method == "POST").Body.Split('&').ShouldBe(
            [
                "phases[0][start_date]=1790000000", "phases[0][end_date]=1800000000",
                "phases[0][items][0][price]=price_base", "phases[0][items][0][quantity]=1",
                "phases[0][items][1][price]=price_seats", "phases[0][items][1][quantity]=11",
                "phases[1][start_date]=1800000000", "phases[1][end_date]=1802592000",
                "phases[1][items][0][price]=price_team", "phases[1][items][0][quantity]=11",
                "proration_behavior=create_prorations",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task FindSubscription_AFeePerPerson_NamesTheCountStripeHolds()
    {
        var stripe = new StripeRequests().Answer("GET", "/v1/subscriptions/sub_1", SeatedJson("month", 10));

        var found = await ProviderOf(stripe).FindSubscriptionAsync("sub_1", Cancellation);

        found!.People.ShouldBe(10);
    }

    [Fact]
    public async Task FindSubscription_WithoutAFeePerPerson_NamesNoCount()
    {
        var stripe = new StripeRequests().Answer("GET", "/v1/subscriptions/sub_1", SubscriptionJson(scheduleId: null));

        var found = await ProviderOf(stripe).FindSubscriptionAsync("sub_1", Cancellation);

        found!.People.ShouldBeNull();
    }

    // B11: an open invoice whose payment Stripe tried and failed names its amount without tax, its attempts, its next attempt and the card.
    [Fact]
    public async Task FindSubscription_AnOpenInvoiceWhosePaymentFailed_NamesItsAttemptsAndTheCard()
    {
        var stripe = new StripeRequests().Answer("GET", "/v1/subscriptions/sub_1", WithInvoice("""
            {"id": "in_1", "object": "invoice", "status": "open", "attempt_count": 2, "next_payment_attempt": 1800259200, "amount_due": 11900,
             "total_excluding_tax": 10000, "currency": "eur", "created": 1799900000, "effective_at": 1800000000}
            """));

        var found = await ProviderOf(stripe).FindSubscriptionAsync("sub_1", Cancellation);

        found!.Payment.ShouldBe(new ProviderPayment(
            "in_1", 10000, "EUR", DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), 2, DateTimeOffset.FromUnixTimeSeconds(1_800_259_200), "visa", "4242"));
        stripe.Sent.Single().Path.ShouldContain("expand[1]=latest_invoice");
    }

    [Fact]
    public async Task FindSubscription_APaidInvoice_NamesNoFailedPayment()
    {
        var stripe = new StripeRequests().Answer("GET", "/v1/subscriptions/sub_1", WithInvoice("""
            {"id": "in_1", "object": "invoice", "status": "paid", "attempt_count": 1, "amount_due": 10000, "currency": "eur", "created": 1799900000}
            """));

        var found = await ProviderOf(stripe).FindSubscriptionAsync("sub_1", Cancellation);

        found!.Payment.ShouldBeNull();
    }

    // B11: an invoice's failed payment, and its payment, ask for the sync of the invoice's subscription.
    [Fact]
    public void ReadEvent_AFailedInvoicePayment_NamesItsSubscription()
    {
        var body = EventBody("evt_8", "invoice.payment_failed", """{"id": "in_8", "object": "invoice", "parent": {"type": "subscription_details", "subscription_details": {"subscription": "sub_8"}}}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, Secret, DateTimeOffset.UtcNow));

        read.ShouldBe(new ProviderEvent("evt_8", "invoice.payment_failed", "sub_8"));
    }

    // B7: a second subscription ends at once with no new invoice, then the paid payment of its latest invoice is refunded in full, under
    // the payment's own idempotency key.
    [Fact]
    public async Task EndAndRefund_ASecondActiveSubscription_EndsItThenRefundsItsPayment()
    {
        var stripe = new StripeRequests()
            .Answer("GET", "/v1/subscriptions/sub_1", """{"id": "sub_1", "object": "subscription", "status": "active", "latest_invoice": "in_1", "items": {"object": "list", "data": []}}""")
            .Answer("DELETE", "/v1/subscriptions/sub_1", """{"id": "sub_1", "object": "subscription", "status": "canceled"}""")
            .Answer("GET", "/v1/invoice_payments", """{"object": "list", "url": "/v1/invoice_payments", "has_more": false, "data": [{"id": "inpay_1", "object": "invoice_payment", "status": "paid", "payment": {"type": "payment_intent", "payment_intent": "pi_1"}}]}""")
            .Answer("POST", "/v1/refunds", """{"id": "re_1", "object": "refund"}""");

        await ProviderOf(stripe).EndAndRefundAsync("sub_1", Cancellation);

        stripe.Sent.Where(request => request.Method != "GET").Select(request => (request.Method, request.Path, request.Body)).ShouldBe(
        [
            ("DELETE", "/v1/subscriptions/sub_1?invoice_now=false&prorate=false", string.Empty),
            ("POST", "/v1/refunds", "payment_intent=pi_1&reason=duplicate"),
        ]);
        stripe.Sent.ShouldContain(request => request.Path.StartsWith("/v1/invoice_payments?invoice=in_1", StringComparison.Ordinal) && request.Path.Contains("status=paid", StringComparison.Ordinal));
    }

    private static string EventBody(string id, string type, string dataObject) =>
        $$$"""{"id": "{{{id}}}", "object": "event", "type": "{{{type}}}", "api_version": "{{{StripeConfiguration.ApiVersion}}}", "created": 1, "data": {"object": {{{dataObject}}} }}""";

    private static string SignatureOf(string body, string secret, DateTimeOffset at)
    {
        var timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    private static StripeRequests Subscribed(string? scheduleId = "sub_sched_1") =>
        new StripeRequests()
            .Answer("GET", "/v1/subscriptions/sub_1", SubscriptionJson(scheduleId))
            .Answer("GET", "/v1/prices", """{"object": "list", "data": [{"id": "price_found", "object": "price"}], "has_more": false, "url": "/v1/prices"}""");

    private static StripeRequests Seated(string interval, int people) =>
        new StripeRequests().Answer("GET", "/v1/subscriptions/sub_1", SeatedJson(interval, people));

    // A subscription on Business with its base fee and its fee per person for this many people, whose period ends at 1 800 000 000.
    private static string SeatedJson(string interval, int people, string schedule = "null") => $$$"""
        {"id": "sub_1", "object": "subscription", "customer": "cus_1", "status": "active", "cancel_at_period_end": false, "schedule": {{{schedule}}},
         "items": {"object": "list", "data": [
           {"id": "si_base", "object": "subscription_item", "quantity": 1, "current_period_end": 1800000000,
            "price": {"id": "price_base", "object": "price", "product": "plan_{{{Business:N}}}", "lookup_key": "plan_{{{Business:N}}}_base_{{{interval}}}_eur_9900", "recurring": {"interval": "{{{interval}}}"} } },
           {"id": "si_seats", "object": "subscription_item", "quantity": {{{people}}}, "current_period_end": 1800000000,
            "price": {"id": "price_seats", "object": "price", "product": "plan_{{{Business:N}}}", "lookup_key": "plan_{{{Business:N}}}_per_person_{{{interval}}}_eur_800", "recurring": {"interval": "{{{interval}}}"} } }]} }
        """;

    // A monthly subscription of ten people on Business whose latest invoice is this one, with the customer's default card, a Visa ending
    // in 4242.
    private static string WithInvoice(string invoice) => $$$"""
        {"id": "sub_1", "object": "subscription", "customer": {"id": "cus_1", "object": "customer", "invoice_settings": {"default_payment_method":
           {"id": "pm_1", "object": "payment_method", "card": {"brand": "visa", "last4": "4242"} } } }, "status": "past_due", "cancel_at_period_end": false,
         "schedule": null, "latest_invoice": {{{invoice}}},
         "items": {"object": "list", "data": [{"id": "si_1", "object": "subscription_item", "quantity": 10, "current_period_end": 1800000000,
           "price": {"id": "price_team_old", "object": "price", "product": "plan_{{{Business:N}}}", "recurring": {"interval": "month"} } }]} }
        """;

    // A monthly subscription of ten people on Business, whose period ends at 1 800 000 000.
    private static string SubscriptionJson(string? scheduleId, string? schedule = null) => $$$"""
        {"id": "sub_1", "object": "subscription", "customer": "cus_1", "status": "active", "cancel_at_period_end": false,
         "schedule": {{{schedule ?? (scheduleId is null ? "null" : $"\"{scheduleId}\"")}}},
         "items": {"object": "list", "data": [{"id": "si_1", "object": "subscription_item", "quantity": 10, "current_period_end": 1800000000,
           "price": {"id": "price_team_old", "object": "price", "product": "plan_{{{Business:N}}}", "recurring": {"interval": "month"} } }]} }
        """;
}
