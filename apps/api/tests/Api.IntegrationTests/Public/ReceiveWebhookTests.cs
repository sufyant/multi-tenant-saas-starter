using System.Net;
using Tenancy;

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

    // B6: a new event of the subscription that changed nothing, or changed it while it stays on the plan, records no second change.
    [Fact]
    public async Task ReceiveWebhook_AnotherEventOfTheSamePlan_RecordsTheChangeOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        _api.Billing.Put(BillingRequests.Subscription(tenant.Id, _planId) with { CancelAtPeriodEnd = true });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenant.Id:N}"));

        (await SubscriptionOfAsync(tenant.Id)).ShouldBe($"cus_{tenant.Id:N}|sub_{tenant.Id:N}|{_planId}|Monthly|Active|t");
        (await _audit.TenantLogAsync(tenant.Id))!.Split(", ").Length.ShouldBe(1);
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

    private Task<Guid> PlanOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenantId}'");

    private Task<string?> SubscriptionOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT concat_ws('|', customer_id, subscription_id, plan_id, interval, status, cancel_at_period_end) FROM billing.subscriptions WHERE tenant_id = '{tenantId}'");
}
