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

    [Fact]
    public async Task GetTenantBilling_AnUnknownTenant_IsNotFound()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var read = await admin.GetTenantBillingAsync(Guid.NewGuid());

        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BillingRequests.CodeOfAsync(read)).ShouldBe("billing.tenant_not_found");
    }
}
