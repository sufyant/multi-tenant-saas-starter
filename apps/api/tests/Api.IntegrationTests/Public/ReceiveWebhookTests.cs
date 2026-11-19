using System.Net;
using Billing.Application.Subscriptions.EndGrace;
using Tenancy;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Public;

// POST /v1/public/billing/webhook: Stripe's signed webhook (A5's exception, step 40). A signed event is kept by its id and answered with
// 204, and its subscription is read again from the provider. The tenant is the one our own map names for the subscription's customer, never
// one that Stripe's metadata names (B5). An active subscription puts the tenant on the plan it pays for, through ControlPlane, and the member
// who opened the checkout is named in both audit logs. An event that arrives again changes nothing.
public sealed class ReceiveWebhookTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private Guid _planId;

    public async ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: BillingRequests.On);
        _planId = await _testData.AddPlanAsync(new() { ["members.max"] = TestData.Unlimited }, name: $"Team {Guid.NewGuid():N}");
        await _testData.SetPlanPricesAsync(_planId, 900, 1000, 9000, 10000);
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ReceiveWebhook_AnActiveSubscription_PutsTheTenantOnItsPlanByTheMemberWhoPaid()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var payer = await _testData.UserIdOfAsync(owner);

        var answer = await _api.PayAsync(owner, tenant.Id, _planId);

        answer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await PlanOfAsync(tenant.Id)).ShouldBe(_planId);
        (await SubscriptionOfAsync(tenant.Id)).ShouldBe($"cus_{tenant.Id:N}|sub_{tenant.Id:N}|{_planId}|Monthly|Active|f");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe($"{tenant.Id}:tenant.plan_changed|User|{payer}|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBe($"tenant.plan_changed|User|{payer}|Tenant|{tenant.Id}|Acme Ltd");
    }

    // Step 40, billing per person: each state of an active subscription brings Stripe's count of seats, and a count that differs from the
    // members who take a seat is set again, whatever order our calls reached Stripe in.
    [Fact]
    public async Task ReceiveWebhook_SeatsThatDifferFromTheMembers_SetsThemAtStripe()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        _api.Billing.Put(BillingRequests.Subscription(tenant.Id, _planId) with { People = 3 });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenant.Id:N}"));

        _api.Billing.Seats.ShouldBe([($"sub_{tenant.Id:N}", 1)]);
    }

    [Fact]
    public async Task ReceiveWebhook_SeatsThatMatchTheMembers_SetsNone()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        _api.Billing.Put(BillingRequests.Subscription(tenant.Id, _planId) with { People = 1 });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenant.Id:N}"));

        _api.Billing.Seats.ShouldBeEmpty();
    }

    // B11, 12.12: a failed payment is an entry of both logs, by nobody, once for each invoice, and the tenant keeps its plan while Stripe
    // retries it.
    [Fact]
    public async Task ReceiveWebhook_AFailedPaymentTwice_RecordsItOnceAndKeepsThePlan()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        var failedAt = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, failedAt, 1, failedAt.AddDays(3)));
        await _api.PaymentFailedAsync(tenant.Id);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, failedAt, 2, failedAt.AddDays(6)));

        await _api.PaymentFailedAsync(tenant.Id);

        (await PlanOfAsync(tenant.Id)).ShouldBe(_planId);
        const string Details = """{"amountDue": 10000, "currency": "EUR", "retriesUntil": "2026-11-29T09:00:00+00:00"}""";
        (await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenant.Id}' AND operation = 'tenant.payment_failed' AND actor_kind = 'Platform' AND details @> '{Details}'")).ShouldBe(1);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenant.Id}' AND operation = 'tenant.payment_failed'")).ShouldBe(1);
    }

    // 13.18, 3.17: a failed payment that Stripe retries tells the members who manage billing, the Owner and the Billing manager, in the
    // tenant's bell and by email, with the card, the amount and the next attempt; a Member is told nothing.
    [Fact]
    public async Task ReceiveWebhook_AFailedPaymentThatStripeRetries_TellsTheMembersWhoManageBilling()
    {
        var tenant = await _testData.AddTenantAsync();
        var email = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email), role: "Owner");
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");
        await _testData.AddMemberAsync(tenant.Id);
        await _api.PayAsync(owner, tenant.Id, _planId);
        var failedAt = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, failedAt, 1, failedAt.AddDays(3)));

        await _api.PaymentFailedAsync(tenant.Id);

        (await ToldAsync(tenant.Id, "tenant.payment_failed")).ShouldBe(string.Join(",", new[] { owner, manager }.Order(StringComparer.Ordinal)));
        var sent = _api.Email.Sent.Single(sent => sent.To == email && sent.Subject == "We couldn’t take the payment for Acme Ltd");
        sent.Text.ShouldContain("Stripe tried Visa ending in 4242 on 15 Nov 2026 for €100 excl. VAT. We’ll try again on 18 Nov 2026.");
        sent.Text.ShouldContain("Update card:");
    }

    // 13.19, 3.18: once Stripe's retries are over, the grace's start is the last warning, once; the failure with no next attempt is no
    // retry to tell of.
    [Fact]
    public async Task ReceiveWebhook_RetriesThatEnded_WarnsTheMembersWhoManageBillingOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var email = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email), role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero), 4, null));
        await _api.PaymentFailedAsync(tenant.Id);

        await _api.PaymentFailedAsync(tenant.Id);

        (await ToldAsync(tenant.Id, "tenant.payment_grace_started")).ShouldBe(owner);
        (await ToldAsync(tenant.Id, "tenant.payment_failed")).ShouldBeNull();
        var warning = _api.Email.Sent.Single(sent => sent.To == email && sent.Subject.StartsWith("Acme Ltd moves to Free on ", StringComparison.Ordinal));
        warning.Text.ShouldContain("We tried Visa ending in 4242 4 times since 1 Nov 2026.");
        warning.Text.ShouldContain("Pay now:");
    }

    // E2, B8: a closed tenant's plan is read-only and its grace moves nothing, so neither its failed payment nor its grace's start tells
    // anyone to pay; both logs still record the failure.
    [Fact]
    public async Task ReceiveWebhook_AFailedPaymentOfAClosedTenant_TellsNobody()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        await database.ScalarAsSuperuserAsync<object>($"UPDATE billing.customers SET closed_at = now() WHERE tenant_id = '{tenant.Id}'");
        var failedAt = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, failedAt, 1, failedAt.AddDays(3)));
        await _api.PaymentFailedAsync(tenant.Id);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, failedAt, 4, null));

        await _api.PaymentFailedAsync(tenant.Id);

        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM notifications.notifications WHERE tenant_id = '{tenant.Id}'")).ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenant.Id}' AND operation = 'tenant.payment_failed'")).ShouldBe(1);
    }

    // 13.18, B14: the failed payment's email goes to the billing email too, in the language of the member who set it, and it writes no bell.
    [Fact]
    public async Task ReceiveWebhook_AFailedPaymentWithABillingEmail_MailsItInTheLanguageOfTheMemberWhoSetIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var accounts = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: PlatformTenants.Email(), language: "tr"), role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).SetBillingEmailAsync(tenant.Id, accounts));
        var failedAt = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, failedAt, 1, failedAt.AddDays(3)));

        await _api.PaymentFailedAsync(tenant.Id);

        _api.Email.Sent.Single(sent => sent.To == accounts).Subject.ShouldBe("Acme Ltd için ödemeyi alamadık");
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM notifications.notifications WHERE tenant_id = '{tenant.Id}'")).ShouldBe(1);
    }

    // B11: once Stripe's retries are over, billing's grace runs for a week, and its end is scheduled; the tenant keeps its plan until then.
    [Fact]
    public async Task ReceiveWebhook_RetriesThatEnded_SchedulesTheGracesEnd()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, DateTimeOffset.UtcNow.AddDays(-14), 4, null));
        var before = DateTimeOffset.UtcNow;

        var session = await _api.TrackMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "invoice.payment_failed", $"sub_{tenant.Id:N}"));

        var scheduled = session.Scheduled.Envelopes().Single(envelope => envelope.Message is EndGraceCommand);
        var end = ((EndGraceCommand)scheduled.Message!).GraceEndsAt;
        (scheduled.Message, scheduled.ScheduledTime).ShouldBe((new EndGraceCommand(tenant.Id, end), end));
        end.ShouldBeInRange(before.AddDays(7), DateTimeOffset.UtcNow.AddDays(7));
        (await PlanOfAsync(tenant.Id)).ShouldBe(_planId);
    }

    // B11: a subscription that a member cancelled ends at its period's end, and the tenant moves to the default plan, by that member.
    [Fact]
    public async Task ReceiveWebhook_TheEndOfACancelledSubscription_MovesTheTenantToTheDefaultPlan()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var payer = await _testData.UserIdOfAsync(owner);
        await _api.PayAsync(owner, tenant.Id, _planId);
        _api.Billing.Put(BillingRequests.Subscription(tenant.Id, _planId) with { CancelAtPeriodEnd = true });
        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenant.Id:N}"));
        _api.Billing.Put(BillingRequests.Subscription(tenant.Id, _planId, "Canceled") with { CancelAtPeriodEnd = true });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.deleted", $"sub_{tenant.Id:N}"));

        (await PlanOfAsync(tenant.Id)).ShouldBe(await DefaultPlanAsync());
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenant.Id}' AND operation = 'tenant.plan_changed' AND actor_id = '{payer}'")).ShouldBe(2);
    }

    // B11: an unpaid subscription that Stripe ends moves the tenant to the default plan, by the platform. The move tells the members who
    // manage billing, with the open invoice (13.20), and no plan change of the platform's.
    [Fact]
    public async Task ReceiveWebhook_AnUnpaidSubscriptionThatStripeEnds_MovesTheTenantToTheDefaultPlanAndTellsTheMembersWhoManageBilling()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        var failedAt = DateTimeOffset.UtcNow.AddDays(-14);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, failedAt, 4, null));
        await _api.PaymentFailedAsync(tenant.Id);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, _planId, failedAt, 4, null) with { Status = "Canceled" });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.deleted", $"sub_{tenant.Id:N}"));

        (await PlanOfAsync(tenant.Id)).ShouldBe(await DefaultPlanAsync());
        (await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenant.Id}' AND operation = 'tenant.moved_to_default_plan' AND actor_kind = 'Platform'")).ShouldBe(1);
        (await ToldAsync(tenant.Id, "tenant.moved_to_default_plan")).ShouldBe(owner);
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT \"values\"->>'amountDue' FROM notifications.notifications WHERE tenant_id = '{tenant.Id}' AND type = 'tenant.moved_to_default_plan'")).ShouldBe("10000 EUR");
        (await ToldAsync(tenant.Id, "tenant.plan_changed")).ShouldBeNull();
    }

    // The bell's text of a plan change says that the platform acted, so a plan a member paid for tells nobody there (step 40).
    [Fact]
    public async Task ReceiveWebhook_AnActiveSubscription_SendsNoPlanChangedNotification()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        await _api.PayAsync(owner, tenant.Id, _planId);

        (await _testData.CountAsync($"SELECT count(*) FROM notifications.notifications WHERE tenant_id = '{tenant.Id}' AND type = 'tenant.plan_changed'")).ShouldBe(0);
    }

    // Stripe, "Webhooks", "Handle duplicate events": the event's id is kept, so a second delivery reads nothing.
    [Fact]
    public async Task ReceiveWebhook_TheSameEventTwice_ReadsTheSubscriptionOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        var eventId = $"evt_{Guid.NewGuid():N}";

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync(eventId, "customer.subscription.updated", $"sub_{tenant.Id:N}"));
        var second = await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync(eventId, "customer.subscription.updated", $"sub_{tenant.Id:N}"));

        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _api.Billing.Reads.Count(read => read == $"sub_{tenant.Id:N}").ShouldBe(2);
    }

    // B6: a new event of the subscription that changed nothing, or changed it while it stays on the plan, records no second change. The
    // end at the period's end is a cancellation of its own (B9).
    [Fact]
    public async Task ReceiveWebhook_AnotherEventOfTheSamePlan_RecordsTheChangeOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        _api.Billing.Put(BillingRequests.Subscription(tenant.Id, _planId) with { CancelAtPeriodEnd = true });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenant.Id:N}"));

        (await SubscriptionOfAsync(tenant.Id)).ShouldBe($"cus_{tenant.Id:N}|sub_{tenant.Id:N}|{_planId}|Monthly|Active|t");
        (await _audit.TenantLogAsync(tenant.Id))!.Split(", ").Count(entry => entry.Contains("tenant.plan_changed|", StringComparison.Ordinal)).ShouldBe(1);
    }

    // B7: a second subscription while the first has not ended came from a page that a race left payable: it ends at Stripe at once, and the
    // tenant keeps the first.
    [Fact]
    public async Task ReceiveWebhook_ASecondActiveSubscription_EndsItAtOnceRefundsItAndKeepsTheFirst()
    {
        var tenant = await _testData.AddTenantAsync();
        await _api.PayAsync(await _testData.AddMemberAsync(tenant.Id, role: "Owner"), tenant.Id, _planId);
        _api.Billing.Put(BillingRequests.Subscription(tenant.Id, _planId) with { SubscriptionId = $"sub_second_{tenant.Id:N}" });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.created", $"sub_second_{tenant.Id:N}"));

        _api.Billing.Refunded.ShouldBe([$"sub_second_{tenant.Id:N}"]);
        _api.Billing.Cancelled.ShouldBeEmpty();
        (await SubscriptionOfAsync(tenant.Id)).ShouldBe($"cus_{tenant.Id:N}|sub_{tenant.Id:N}|{_planId}|Monthly|Active|f");
        (await _audit.TenantLogAsync(tenant.Id))!.Split(", ").Length.ShouldBe(1);
    }

    // B8: a subscription that arrives after the closure, from a page paid in between, ends at its period's end too.
    [Fact]
    public async Task ReceiveWebhook_ASubscriptionOfATenantThatBillingKnowsClosed_EndsItAtThePeriodsEnd()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        (await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, _planId)).EnsureSuccessStatusCode();
        await database.ScalarAsSuperuserAsync<object>($"UPDATE billing.customers SET closed_at = now() WHERE tenant_id = '{tenant.Id}'");
        _api.Billing.Put(BillingRequests.Subscription(tenant.Id, _planId));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "checkout.session.completed", $"sub_{tenant.Id:N}"));

        _api.Billing.PeriodEnds.ShouldBe([($"sub_{tenant.Id:N}", true)]);
    }

    // Only an active subscription pays for its plan (Stripe, "Subscription statuses"): an unpaid first invoice keeps the tenant's plan.
    [Fact]
    public async Task ReceiveWebhook_AnIncompleteSubscription_KeepsItButNotThePlan()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var before = await PlanOfAsync(tenant.Id);

        await _api.PayAsync(owner, tenant.Id, _planId, status: "Incomplete");

        (await PlanOfAsync(tenant.Id)).ShouldBe(before);
        (await SubscriptionOfAsync(tenant.Id)).ShouldBe($"cus_{tenant.Id:N}|sub_{tenant.Id:N}|{_planId}|Monthly|Incomplete|f");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBeNull();
    }

    // B5: the tenant comes from our own map, so a subscription of a customer that is not in it changes no tenant.
    [Fact]
    public async Task ReceiveWebhook_ACustomerThatIsNotOurs_ChangesNoTenant()
    {
        var tenant = await _testData.AddTenantAsync();
        var before = await PlanOfAsync(tenant.Id);
        _api.Billing.Put(BillingRequests.Subscription(tenant.Id, _planId));

        var answer = await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.created", $"sub_{tenant.Id:N}"));

        answer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await PlanOfAsync(tenant.Id)).ShouldBe(before);
        (await SubscriptionOfAsync(tenant.Id)).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("t=1,v1=forged")]
    public async Task ReceiveWebhook_ABodyWithoutStripesSignature_IsRefusedAndKeepsNothing(string signature)
    {
        var eventId = $"evt_{Guid.NewGuid():N}";

        var answer = await _api.CreateClient().SendWebhookAsync(eventId, "customer.subscription.created", "sub_forged", signature);

        answer.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.webhook_invalid");
        (await _testData.CountAsync($"SELECT count(*) FROM billing.stripe_events WHERE id = '{eventId}'")).ShouldBe(0);
        _api.Billing.Reads.ShouldNotContain("sub_forged");
    }

    // A subscription that pays for no plan of ours is not ours: the event is kept and nothing follows.
    [Fact]
    public async Task ReceiveWebhook_ASubscriptionThatIsNotOurs_AnswersAndChangesNothing()
    {
        var eventId = $"evt_{Guid.NewGuid():N}";

        var answer = await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync(eventId, "customer.subscription.updated", "sub_someone_else"));

        answer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _testData.CountAsync($"SELECT count(*) FROM billing.stripe_events WHERE id = '{eventId}'")).ShouldBe(1);
        _api.Billing.Reads.ShouldContain("sub_someone_else");
    }

    [Fact]
    public async Task ReceiveWebhook_BillingOff_IsNotFound()
    {
        await using var off = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));

        var answer = await off.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.created", "sub_any");

        answer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.off");
    }

    private Task<Guid> DefaultPlanAsync() => database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans");

    private Task<Guid> PlanOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenantId}'");

    private Task<string?> SubscriptionOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT concat_ws('|', customer_id, subscription_id, plan_id, interval, status, cancel_at_period_end) FROM billing.subscriptions WHERE tenant_id = '{tenantId}'");

    // The external ids of the users that the tenant's notifications of the type went to, in order; none when none went.
    private Task<string?> ToldAsync(Guid tenantId, string type) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(users.external_id, ',' ORDER BY users.external_id COLLATE \"C\") FROM notifications.notifications JOIN control_plane.users ON users.id = notifications.user_id WHERE notifications.tenant_id = '{tenantId}' AND notifications.type = '{type}'");
}
