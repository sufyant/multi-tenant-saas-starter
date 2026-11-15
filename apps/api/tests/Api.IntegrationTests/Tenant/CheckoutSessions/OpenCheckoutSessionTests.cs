using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.CheckoutSessions;

// POST /v1/tenants/{tenantId}/billing/checkout-session: a member who manages billing opens Stripe Checkout for a plan (step 40). The order
// pays the plan's base fee once and its fee per person for each member who takes a seat; the Billing manager takes none. Our app stores
// nothing until the signed event of the payment.
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
        (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("url").GetString().ShouldBe(FakeBillingProvider.CheckoutLink.ToString());
        var order = _api.Billing.Checkouts.ShouldHaveSingleItem();
        (order.TenantId, order.PaidBy, order.PlanId, order.Currency, order.Yearly).ShouldBe((tenant.Id, await _testData.UserIdOfAsync(owner), _planId, "EUR", false));
        (order.BaseAmount, order.PerPersonAmount, order.People, order.CustomerId).ShouldBe((900L, 1000L, 2, null));
        (order.SuccessUrl.ToString(), order.CancelUrl.ToString()).ShouldBe(($"{BillingRequests.ReturnUrl}?checkout=success", $"{BillingRequests.ReturnUrl}?checkout=cancelled"));
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
        _api.Billing.Put(BillingRequests.ActiveSubscription(tenant.Id, _planId, await _testData.UserIdOfAsync(owner)));
        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.created", $"sub_{tenant.Id:N}"));

        var answer = await _api.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, _planId);

        answer.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.already_subscribed");
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
}
