using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Subscriptions;

// GET /v1/tenants/{tenantId}/billing: a member who manages billing reads the tenant's subscription as billing last read it from Stripe
// (step 40). Row level security keeps the read to the tenant of the route.
public sealed class GetBillingTests(Database database) : IAsyncLifetime
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
    public async Task GetBilling_BeforeTheFirstPayment_AnswersNoSubscription()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await _api.CreateClient(owner).GetBillingAsync(tenant.Id);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await answer.Content.ReadAsStringAsync(Cancellation)).ShouldBe("""{"subscription":null}""");
    }

    [Fact]
    public async Task GetBilling_AfterThePayment_AnswersTheSubscription()
    {
        var planId = await _testData.AddPlanAsync([]);
        var tenant = await _testData.AddTenantAsync();
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");
        _api.Billing.Put(BillingRequests.ActiveSubscription(tenant.Id, planId, await _testData.UserIdOfAsync(manager)));
        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.created", $"sub_{tenant.Id:N}"));

        var answer = await _api.CreateClient(manager).GetBillingAsync(tenant.Id);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subscription = (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("subscription");
        (subscription.GetProperty("planId").GetGuid(), subscription.GetProperty("interval").GetString(), subscription.GetProperty("status").GetString())
            .ShouldBe((planId, "monthly", "active"));
        (subscription.GetProperty("currentPeriodEnd").GetDateTimeOffset(), subscription.GetProperty("cancelAtPeriodEnd").GetBoolean())
            .ShouldBe((new DateTimeOffset(2026, 12, 15, 0, 0, 0, TimeSpan.Zero), false));
    }

    // Row level security: another tenant's subscription is not this tenant's.
    [Fact]
    public async Task GetBilling_AnotherTenantPays_AnswersNoSubscriptionHere()
    {
        var planId = await _testData.AddPlanAsync([]);
        var paying = await _testData.AddTenantAsync();
        _api.Billing.Put(BillingRequests.ActiveSubscription(paying.Id, planId, await _testData.UserIdOfAsync(await _testData.AddMemberAsync(paying.Id, role: "Owner"))));
        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.created", $"sub_{paying.Id:N}"));
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await _api.CreateClient(owner).GetBillingAsync(tenant.Id);

        (await answer.Content.ReadAsStringAsync(Cancellation)).ShouldBe("""{"subscription":null}""");
    }

    [Fact]
    public async Task GetBilling_ByAMember_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);

        var answer = await _api.CreateClient(member).GetBillingAsync(tenant.Id);

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
