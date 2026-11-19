using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.CheckoutSessions;

// POST /v1/tenants/{tenantId}/billing/checkout-session: a member who manages billing opens Stripe Checkout for a plan (step 40). The order
// pays the plan's base fee once and its fee per person for each member who takes a seat; the Billing manager takes none. Our app keeps only
// the tenant's customer in its own map, once (B5), until the signed event of the payment.
public sealed class OpenCheckoutSessionTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private Guid _planId;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: BillingRequests.On);
        _planId = await _testData.AddPlanAsync(new() { ["members.max"] = TestData.Unlimited }, name: $"Team {Guid.NewGuid():N}");
        await _testData.SetPlanPricesAsync(_planId, baseMonthly: 900, perPersonMonthly: 1000, baseYearly: 9000, perPersonYearly: 10000);
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task OpenCheckoutSession_ByAnOwner_AnswersStripesPageForTheSeatedMembers()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");

        var answer = await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, _planId);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = _api.Billing.Checkouts.ShouldHaveSingleItem();
        (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("url").GetString().ShouldBe(FakeBillingProvider.PageOf(order.Key).ToString());
        (order.TenantId, order.PaidBy, order.PlanId, order.Currency, order.Yearly).ShouldBe((tenant.Id, await _testData.UserIdOfAsync(owner), _planId, "EUR", false));
        (order.BaseAmount, order.PerPersonAmount, order.People, order.CustomerId).ShouldBe((900L, 1000L, 2, $"cus_{tenant.Id:N}"));
        _api.Billing.Customers.ShouldBe([tenant.Id]);
        (await CustomerMapOfAsync(tenant.Id)).ShouldBe($"cus_{tenant.Id:N}|{await _testData.UserIdOfAsync(owner)}");
        (order.SuccessUrl.ToString(), order.CancelUrl.ToString()).ShouldBe(($"{BillingRequests.ReturnUrl}?checkout=success", $"{BillingRequests.ReturnUrl}?checkout=cancelled"));
    }

    // B14: a tenant that set its billing email before its first payment gets a customer with it, so Stripe's invoices go there.
    [Fact]
    public async Task OpenCheckoutSession_AfterABillingEmail_CreatesTheCustomerWithIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var client = _api.CreateClient(owner);
        var accounts = PlatformTenants.Email();
        await client.SetBillingEmailAsync(tenant.Id, accounts);

        (await client.OpenCheckoutSessionAsync(tenant.Id, _planId)).EnsureSuccessStatusCode();

        _api.Billing.CustomerEmails.ShouldBe([(FakeBillingProvider.CustomerOf(tenant.Id), accounts)]);
    }

    [Fact]
    public async Task OpenCheckoutSession_Yearly_OrdersTheYearlyPrices()
    {
        var tenant = await _testData.AddTenantAsync();
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await _api.CreateClient(manager).OpenCheckoutSessionAsync(tenant.Id, _planId, interval: "yearly");

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = _api.Billing.Checkouts.ShouldHaveSingleItem();
        (order.Yearly, order.BaseAmount, order.PerPersonAmount, order.People).ShouldBe((true, 9000L, 10000L, 1));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Member")]
    public async Task OpenCheckoutSession_ByARoleWithoutBilling_IsForbiddenAndOpensNothing(string role)
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id, role: role);

        var answer = await _api.CreateClient(member).OpenCheckoutSessionAsync(tenant.Id, _planId);

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        _api.Billing.Checkouts.ShouldBeEmpty();
    }

    // OWASP, "Unvalidated Redirects and Forwards": Stripe sends the person back only to a page of the web app.
    [Theory]
    [InlineData("https://evil.test/acme/settings/billing")]
    [InlineData("https://app.test/acme/settings/billing?next=https://evil.test")]
    public async Task OpenCheckoutSession_AnAddressOutsideTheWebApp_IsRefused(string returnUrl)
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, _planId, returnUrl: returnUrl);

        answer.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.return_url_invalid");
        _api.Billing.Checkouts.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenCheckoutSession_APlanThatCostsNothing_IsUnprocessable()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var free = await _testData.AddPlanAsync([]);

        var answer = await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, free);

        answer.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.plan_free");
    }

    [Fact]
    public async Task OpenCheckoutSession_AnUnknownPlan_IsNotFound()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, Guid.NewGuid());

        answer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.plan_not_found");
    }

    // A tenant that pays by card changes its plan instead; a new checkout would start a second subscription.
    [Fact]
    public async Task OpenCheckoutSession_ATenantThatPaysByCard_IsAConflict()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);

        var answer = await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, _planId);

        answer.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.already_subscribed");
    }

    // B5 and B7: the tenant has one customer and one open checkout. A second checkout of the same order, by another member, gets the same
    // page and names the new member.
    [Fact]
    public async Task OpenCheckoutSession_ASecondTime_GetsTheSamePageAndNamesTheNewMember()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");
        var first = await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, _planId);

        var second = await _api.CreateClient(manager).OpenCheckoutSessionAsync(tenant.Id, _planId);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await UrlOfAsync(second)).ShouldBe(await UrlOfAsync(first));
        _api.Billing.Customers.ShouldBe([tenant.Id]);
        _api.Billing.Checkouts.ShouldHaveSingleItem().CustomerId.ShouldBe($"cus_{tenant.Id:N}");
        (await CustomerMapOfAsync(tenant.Id)).ShouldBe($"cus_{tenant.Id:N}|{await _testData.UserIdOfAsync(manager)}");
    }

    // B7: two first checkouts at once, by two members, open one page at Stripe, so the tenant cannot pay twice.
    [Fact]
    public async Task OpenCheckoutSession_TwoFirstCheckoutsAtOnce_OpenOnePage()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var manager = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "BillingManager"));

        var answers = await Task.WhenAll(owner.OpenCheckoutSessionAsync(tenant.Id, _planId), manager.OpenCheckoutSessionAsync(tenant.Id, _planId));

        answers.Select(answer => answer.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.OK]);
        (await UrlOfAsync(answers[1])).ShouldBe(await UrlOfAsync(answers[0]));
        _api.Billing.Checkouts.Select(order => order.Key).Distinct().ShouldHaveSingleItem();
        _api.Billing.Customers.Distinct().ShouldBe([tenant.Id]);
    }

    // B7: a checkout of another order replaces the open one, and Stripe expires the pages of the replaced key.
    [Fact]
    public async Task OpenCheckoutSession_AnotherOrder_ReplacesTheOpenPageAndExpiresIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        await owner.OpenCheckoutSessionAsync(tenant.Id, _planId);
        var monthly = _api.Billing.Checkouts.ShouldHaveSingleItem().Key;

        var yearly = await _api.WaitingForMessagesAsync(() => owner.OpenCheckoutSessionAsync(tenant.Id, _planId, interval: "yearly"));

        var kept = _api.Billing.Checkouts.Last().Key;
        (await UrlOfAsync(yearly)).ShouldBe(FakeBillingProvider.PageOf(kept).ToString());
        kept.ShouldNotBe(monthly);
        _api.Billing.Expiries.ShouldBe([($"cus_{tenant.Id:N}", monthly)]);
    }

    // B7: a subscription that pays ends the tenant's open checkout and expires its page; a new checkout is then refused.
    [Fact]
    public async Task OpenCheckoutSession_AfterThePayment_TheOpenPageIsExpiredAndANewOneIsRefused()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");

        await _api.PayAsync(owner, tenant.Id, _planId);
        var again = await _api.CreateClient(manager).OpenCheckoutSessionAsync(tenant.Id, _planId);

        _api.Billing.Expiries.ShouldBe([($"cus_{tenant.Id:N}", _api.Billing.Checkouts.ShouldHaveSingleItem().Key)]);
        (await _testData.CountAsync($"SELECT count(*) FROM billing.checkouts WHERE tenant_id = '{tenant.Id}'")).ShouldBe(0);
        (await BillingRequests.CodeOfAsync(again)).ShouldBe("billing.already_subscribed");
    }

    // B7: a page made under a key that another order replaced while Stripe made it is expired, and the person is not sent to it.
    [Fact]
    public async Task OpenCheckoutSession_ReplacedWhileItsPageWasMade_IsAConflictAndExpiresThePage()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        _api.Billing.WhileOpeningCheckout = () => owner.OpenCheckoutSessionAsync(tenant.Id, _planId, interval: "yearly");

        var first = await _api.WaitingForMessagesAsync(() => owner.OpenCheckoutSessionAsync(tenant.Id, _planId));

        first.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(first)).ShouldBe("billing.checkout_replaced");
        var replaced = _api.Billing.Checkouts.First().Key;
        _api.Billing.Expiries.ShouldContain(($"cus_{tenant.Id:N}", replaced));
        (await database.ScalarAsSuperuserAsync<Guid>($"SELECT key FROM billing.checkouts WHERE tenant_id = '{tenant.Id}'")).ShouldNotBe(replaced);
    }

    // B8: a closed tenant opens no checkout, though its member passed the door before the closure.
    [Fact]
    public async Task OpenCheckoutSession_ATenantThatBillingKnowsClosed_IsAConflict()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        await owner.OpenCheckoutSessionAsync(tenant.Id, _planId);
        await database.ScalarAsSuperuserAsync<object>($"UPDATE billing.customers SET closed_at = now() WHERE tenant_id = '{tenant.Id}'");

        var again = await owner.OpenCheckoutSessionAsync(tenant.Id, _planId, interval: "yearly");

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(again)).ShouldBe("billing.tenant_closed");
    }

    // A refused checkout creates no customer.
    [Fact]
    public async Task OpenCheckoutSession_Refused_CreatesNoCustomer()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, _planId, returnUrl: "https://evil.test/");

        _api.Billing.Customers.ShouldBeEmpty();
        (await CustomerMapOfAsync(tenant.Id)).ShouldBeNull();
    }

    // The maintainer's decision of 2026-11-14: billing is off by default, and the app works as before step 40.
    [Fact]
    public async Task OpenCheckoutSession_BillingOff_IsNotFound()
    {
        await using var off = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await off.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, _planId);

        answer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.off");
    }

    private static async Task<string?> UrlOfAsync(HttpResponseMessage answer) =>
        (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("url").GetString();

    private Task<string?> CustomerMapOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT concat_ws('|', customer_id, checkout_by) FROM billing.customers WHERE tenant_id = '{tenantId}'");
}
