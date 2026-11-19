using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Subscriptions;

// B12, 4.31 to 4.35: the platform reads how a tenant pays and its subscription as billing last read it.
public sealed class GetTenantBillingTests(Database database) : IAsyncLifetime
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
    public async Task GetTenantBilling_APayingTenant_AnswersCardAndItsSubscription()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var read = await admin.GetTenantBillingAsync(tenantId);

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        var billing = await read.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        billing.GetProperty("billedBy").GetString().ShouldBe("card");
        var subscription = billing.GetProperty("subscription");
        (subscription.GetProperty("planId").GetGuid(), subscription.GetProperty("currentPeriodEnd").GetDateTimeOffset()).ShouldBe((_tenants.Team, PayingTenants.PeriodEnd));
    }

    // 4.27: the platform names the tenant's customer at Stripe and its page in Stripe's dashboard.
    [Fact]
    public async Task GetTenantBilling_APayingTenant_AnswersItsCustomerAndItsDashboardPage()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var billing = await (await admin.GetTenantBillingAsync(tenantId)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        var customer = billing.GetProperty("customer");
        $"{customer.GetProperty("id").GetString()}|{customer.GetProperty("dashboardUrl").GetString()}"
            .ShouldBe($"{FakeBillingProvider.CustomerOf(tenantId)}|https://dashboard.example/customers/{FakeBillingProvider.CustomerOf(tenantId)}");
    }

    // 4.27: the last payment is when Stripe last named a paid invoice; an open invoice after it keeps it.
    [Fact]
    public async Task GetTenantBilling_AnOpenInvoiceAfterAPaidOne_AnswersTheLastPayment()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var paidAt = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
        _api.Billing.Put(BillingRequests.Subscription(tenantId, _tenants.Team) with { LastPaidAt = paidAt });
        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "invoice.paid", $"sub_{tenantId:N}"));
        _api.Billing.Put(BillingRequests.Subscription(tenantId, _tenants.Team) with { CurrentPeriodEnd = new DateTimeOffset(2027, 1, 15, 0, 0, 0, TimeSpan.Zero) });
        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenantId:N}"));
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var billing = await (await admin.GetTenantBillingAsync(tenantId)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        billing.GetProperty("subscription").GetProperty("lastPaymentAt").GetDateTimeOffset().ShouldBe(paidAt);
    }

    [Fact]
    public async Task GetTenantBilling_ATenantThatNeverOpenedACheckout_AnswersNoCustomer()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var billing = await (await admin.GetTenantBillingAsync(tenant.Id)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        billing.GetProperty("customer").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetTenantBilling_AnUnknownTenant_IsNotFound()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var read = await admin.GetTenantBillingAsync(Guid.NewGuid());

        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BillingRequests.CodeOfAsync(read)).ShouldBe("billing.tenant_not_found");
    }
}
