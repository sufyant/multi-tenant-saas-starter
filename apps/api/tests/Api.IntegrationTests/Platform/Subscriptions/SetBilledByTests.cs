using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Subscriptions;

// B12, 4.34, 4.35 and 10.19: a system admin marks a tenant Contract, or takes it back to card. A Contract tenant is billed outside the app:
// its subscription ends at the end of the period it paid for, with no refund, its members open no checkout and change no plan, and a system
// admin sets its plan. Both logs record each switch (12.12, 7.11).
public sealed class SetBilledByTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private PayingTenants _tenants = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: BillingRequests.On);
        _tenants = new PayingTenants(database, _api);
        await _tenants.AddPlansAsync();
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task SetBilledBy_ContractForAPayingTenant_EndsItsSubscriptionAtThePeriodsEndAndRecordsItInBothLogs()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var adminId = await _testData.AddSystemAdminAsync();
        var admin = _api.CreateClient(adminId, secondFactor: true);

        var set = await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await set.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("billedBy").GetString().ShouldBe("contract");
        _api.Billing.PeriodEnds.ShouldBe([($"sub_{tenantId:N}", true)]);
        _api.Billing.Cancelled.ShouldBeEmpty();
        _api.Billing.Refunded.ShouldBeEmpty();
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = 'tenant.contract_started' AND actor_kind = 'Platform'")).ShouldBe(1);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}' AND operation = 'tenant.contract_started' AND actor_kind = 'User'")).ShouldBe(1);
    }

    // The end that Contract asked for is the platform's, not a member's: the Activity log records no cancellation (B9).
    [Fact]
    public async Task SetBilledBy_ContractThenStripesEventOfTheEnd_RecordsNoCancellation()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenantId:N}"));

        (await _tenants.SubscriptionOfAsync(tenantId)).ShouldBe($"{_tenants.Team}|Monthly|t||");
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = 'tenant.plan_cancelled'")).ShouldBe(0);
        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Team);
    }

    [Fact]
    public async Task SetBilledBy_ContractForATenantThatNeverPaid_AsksStripeNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var set = await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenant.Id, "contract"));

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        var billing = await set.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (billing.GetProperty("billedBy").GetString(), billing.GetProperty("subscription").ValueKind).ShouldBe(("contract", JsonValueKind.Null));
        _api.Billing.PeriodEnds.ShouldBeEmpty();
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenant.Id}' AND operation = 'tenant.contract_started'")).ShouldBe(1);
    }

    // The state the tenant has already answers 200 and records nothing (I1).
    [Fact]
    public async Task SetBilledBy_ContractTwice_RecordsItOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenant.Id, "contract"));

        var again = await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenant.Id, "contract"));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenant.Id}' AND operation = 'tenant.contract_started'")).ShouldBe(1);
    }

    // A card tenant that is a card tenant already changes nothing.
    [Fact]
    public async Task SetBilledBy_CardForACardTenant_RecordsNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var set = await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenant.Id, "card"));

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenant.Id}' AND operation LIKE 'tenant.contract_%'")).ShouldBe(0);
    }

    // Back to card before the period's end: the end that Contract asked for is taken back, as a reopen takes back a closure's (B8).
    [Fact]
    public async Task SetBilledBy_CardBeforeThePeriodsEnd_KeepsTheSubscription()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));

        var set = await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "card"));

        (await set.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("billedBy").GetString().ShouldBe("card");
        _api.Billing.PeriodEnds.ShouldBe([($"sub_{tenantId:N}", true), ($"sub_{tenantId:N}", false)]);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = 'tenant.contract_ended' AND actor_kind = 'Platform'")).ShouldBe(1);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}' AND operation = 'tenant.contract_ended' AND actor_kind = 'User'")).ShouldBe(1);
    }

    // A closed tenant stays without card billing: the end that the closure asked for stays (B8).
    [Fact]
    public async Task SetBilledBy_CardForAClosedTenant_KeepsTheEnd()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));
        await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenantId));

        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "card"));

        _api.Billing.PeriodEnds.ShouldBe([($"sub_{tenantId:N}", true)]);
    }

    // A reopen takes back no end while the tenant is Contract.
    [Fact]
    public async Task SetBilledBy_ContractThenAReopen_KeepsTheEnd()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenantId));
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));

        await _api.WaitingForMessagesAsync(() => admin.ReopenTenantAsync(tenantId));

        _api.Billing.PeriodEnds.ShouldBe([($"sub_{tenantId:N}", true)]);
    }

    [Fact]
    public async Task SetBilledBy_AContractTenantsOpenCheckout_ExpiresItsPage()
    {
        var tenant = await _testData.AddTenantAsync();
        await _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner")).OpenCheckoutSessionAsync(tenant.Id, _tenants.Team);
        var key = _api.Billing.Checkouts.ShouldHaveSingleItem().Key;
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenant.Id, "contract"));

        _api.Billing.Expiries.ShouldBe([($"cus_{tenant.Id:N}", key)]);
        (await _testData.CountAsync($"SELECT count(*) FROM billing.checkouts WHERE tenant_id = '{tenant.Id}'")).ShouldBe(0);
    }

    [Fact]
    public async Task SetBilledBy_AnUnknownTenant_IsNotFound()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var set = await admin.SetBilledByAsync(Guid.NewGuid(), "contract");

        set.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BillingRequests.CodeOfAsync(set)).ShouldBe("billing.tenant_not_found");
    }

    // 10.19: the members who manage billing read that the tenant is billed outside the app.
    [Fact]
    public async Task SetBilledBy_Contract_TheMembersReadContract()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));

        var read = await _api.CreateClient(manager).GetBillingAsync(tenantId);

        (await read.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("billedBy").GetString().ShouldBe("contract");
    }

    [Fact]
    public async Task SetBilledBy_NeverSet_TheMembersReadCard()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var read = await _api.CreateClient(owner).GetBillingAsync(tenant.Id);

        (await read.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("billedBy").GetString().ShouldBe("card");
    }

    [Fact]
    public async Task SetBilledBy_ContractThenACheckout_IsAConflict()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenant.Id, "contract"));

        var opened = await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, _tenants.Team);

        opened.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(opened)).ShouldBe("billing.contract");
        _api.Billing.Customers.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetBilledBy_ContractThenAPlanChange_IsAConflict()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));

        var changed = await _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Business);

        changed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(changed)).ShouldBe("billing.contract");
        _api.Billing.MovesNow.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetBilledBy_ContractThenAPreview_IsAConflict()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));

        var preview = await _api.CreateClient(manager).PreviewPlanChangeAsync(tenantId, _tenants.Business);

        (await BillingRequests.CodeOfAsync(preview)).ShouldBe("billing.contract");
    }

    [Fact]
    public async Task SetBilledBy_ContractThenAnUndo_IsAConflict()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));

        var undone = await _api.CreateClient(manager).UndoScheduledChangeAsync(tenantId);

        (await BillingRequests.CodeOfAsync(undone)).ShouldBe("billing.contract");
        _api.Billing.Undone.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetBilledBy_ContractThenThePortal_IsAConflict()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));

        var opened = await _api.CreateClient(manager).OpenPortalSessionAsync(tenantId);

        (await BillingRequests.CodeOfAsync(opened)).ShouldBe("billing.contract");
        _api.Billing.Portals.ShouldBeEmpty();
    }

    // A system admin sets a Contract tenant's plan: the end of its last subscription moves it nowhere (E2).
    [Fact]
    public async Task SetBilledBy_ContractThenTheEndOfTheSubscription_KeepsThePlan()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));
        _api.Billing.Put(BillingRequests.Subscription(tenantId, _tenants.Team, "Canceled") with { CancelAtPeriodEnd = true });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.deleted", $"sub_{tenantId:N}"));

        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Team);
    }

    // A system admin's plan of a Contract tenant stays: a state of its last subscription, while it runs out, puts the tenant back on no plan.
    [Fact]
    public async Task SetBilledBy_ContractThenAStateOfTheSubscription_KeepsThePlanAnAdminSet()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));
        await _api.WaitingForMessagesAsync(() => admin.PutAsJsonAsync($"/v1/platform/tenants/{tenantId}/plan", new { planId = _tenants.Business }, Cancellation));
        _api.Billing.Put(BillingRequests.Subscription(tenantId, _tenants.Team) with { CancelAtPeriodEnd = true, CurrentPeriodEnd = PayingTenants.PeriodEnd.AddDays(1) });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenantId:N}"));

        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Business);
    }

    // B10: a Contract tenant pays no card for its seats.
    [Fact]
    public async Task SetBilledBy_ContractThenSeatsThatDiffer_SetsNone()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));
        _api.Billing.Put(BillingRequests.Subscription(tenantId, _tenants.Team) with { CancelAtPeriodEnd = true, People = 7 });

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenantId:N}"));

        _api.Billing.Seats.ShouldBeEmpty();
    }

    // B11: a Contract tenant has no failed-payment states: a failure records nothing and starts no grace.
    [Fact]
    public async Task SetBilledBy_ContractThenAFailedPayment_RecordsNothing()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));
        _api.Billing.Put(BillingRequests.PastDue(tenantId, _tenants.Team, DateTimeOffset.UtcNow.AddDays(-14), 4, null) with { CancelAtPeriodEnd = true });

        await _api.PaymentFailedAsync(tenantId);

        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = 'tenant.payment_failed'")).ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM billing.subscriptions WHERE tenant_id = '{tenantId}' AND grace_ends_at IS NOT NULL")).ShouldBe(0);
    }
}
