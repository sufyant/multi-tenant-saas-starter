using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.PortalSessions;

// POST /v1/tenants/{tenantId}/billing/portal-session: a member who manages billing opens Stripe's customer portal, for the tenant's own
// customer (step 40). A tenant that has never paid by card has none.
public sealed class OpenPortalSessionTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: BillingRequests.On);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task OpenPortalSession_ATenantThatPaid_AnswersThePortalOfItsCustomer()
    {
        var planId = await _testData.AddPlanAsync([]);
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        _api.Billing.Put(BillingRequests.ActiveSubscription(tenant.Id, planId, await _testData.UserIdOfAsync(owner)));
        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.created", $"sub_{tenant.Id:N}"));

        var answer = await _api.CreateClient(owner).OpenPortalSessionAsync(tenant.Id);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("url").GetString().ShouldBe(FakeBillingProvider.PortalLink.ToString());
        _api.Billing.Portals.ShouldHaveSingleItem().ShouldBe(($"cus_{tenant.Id:N}", new Uri(BillingRequests.ReturnUrl)));
    }

    [Fact]
    public async Task OpenPortalSession_ATenantThatNeverPaid_IsAConflict()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await _api.CreateClient(owner).OpenPortalSessionAsync(tenant.Id);

        answer.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.no_customer");
        _api.Billing.Portals.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenPortalSession_AnAddressOutsideTheWebApp_IsRefused()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await _api.CreateClient(owner).OpenPortalSessionAsync(tenant.Id, "https://evil.test/");

        answer.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.return_url_invalid");
    }
}
