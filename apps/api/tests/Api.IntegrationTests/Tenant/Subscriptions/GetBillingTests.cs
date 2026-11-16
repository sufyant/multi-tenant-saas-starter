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
        (await answer.Content.ReadAsStringAsync(Cancellation)).ShouldBe("""{"billedBy":"card","subscription":null}""");
    }

    [Fact]
    public async Task GetBilling_AfterThePayment_AnswersTheSubscription()
    {
        var planId = await PricedPlanAsync();
        var tenant = await _testData.AddTenantAsync();
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(manager, tenant.Id, planId);

        var answer = await _api.CreateClient(manager).GetBillingAsync(tenant.Id);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subscription = (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("subscription");
        (subscription.GetProperty("planId").GetGuid(), subscription.GetProperty("interval").GetString(), subscription.GetProperty("status").GetString())
            .ShouldBe((planId, "monthly", "active"));
        (subscription.GetProperty("currentPeriodEnd").GetDateTimeOffset(), subscription.GetProperty("cancelAtPeriodEnd").GetBoolean())
            .ShouldBe((new DateTimeOffset(2026, 12, 15, 0, 0, 0, TimeSpan.Zero), false));
    }

    // B11, 10.15: while Stripe retries a failed payment, the subscription answers when it failed, how often, the next attempt, the amount
    // due without tax and the card it tried.
    [Fact]
    public async Task GetBilling_AFailedPaymentThatStripeRetries_AnswersRetrying()
    {
        var planId = await PricedPlanAsync();
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, planId);
        var failedAt = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        _api.Billing.Put(BillingRequests.PastDue(tenant.Id, planId, failedAt, 2, failedAt.AddDays(3)));
        await _api.PaymentFailedAsync(tenant.Id);

        var answer = await _api.CreateClient(owner).GetBillingAsync(tenant.Id);

        var payment = (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("subscription").GetProperty("payment");
        payment.GetRawText().ShouldBe(
            """{"state":"retrying","failedAt":"2026-11-15T09:00:00+00:00","attempts":2,"nextAttemptAt":"2026-11-18T09:00:00+00:00","graceEndsAt":null,"movedAt":null,"amountDue":10000,"currency":"EUR","card":{"brand":"visa","last4":"4242"}}""");
    }

    // Row level security: another tenant's subscription is not this tenant's.
    [Fact]
    public async Task GetBilling_AnotherTenantPays_AnswersNoSubscriptionHere()
    {
        var planId = await PricedPlanAsync();
        var paying = await _testData.AddTenantAsync();
        await _api.PayAsync(await _testData.AddMemberAsync(paying.Id, role: "Owner"), paying.Id, planId);
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await _api.CreateClient(owner).GetBillingAsync(tenant.Id);

        (await answer.Content.ReadAsStringAsync(Cancellation)).ShouldBe("""{"billedBy":"card","subscription":null}""");
    }

    [Fact]
    public async Task GetBilling_ByAMember_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);

        var answer = await _api.CreateClient(member).GetBillingAsync(tenant.Id);

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> PricedPlanAsync()
    {
        var planId = await _testData.AddPlanAsync([]);
        await _testData.SetPlanPricesAsync(planId, 900, 1000, 9000, 10000);
        return planId;
    }
}
