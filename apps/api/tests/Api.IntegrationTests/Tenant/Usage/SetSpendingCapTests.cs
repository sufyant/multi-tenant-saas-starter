using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Plans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Usage;

// PUT /v1/tenants/{tenantId}/usage/spending-cap (11.02, 11.03): a member who manages billing sets the tenant's optional monthly cap on the
// extra of its charge meters, in minor units excl. VAT, or removes it with none. Only a tenant that pays by card has a cap: billing off and
// Contract charge no extra in the app (E9).
public sealed class SetSpendingCapTests(Database database) : IAsyncLifetime
{
    private const string Meter = "ai.requests";

    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = Api(BillingRequests.On);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task SetSpendingCap_ForATenantThatPaysByCard_IsTheCapOfUsageAndRecorded()
    {
        var (tenant, manager) = await PayingTenantAsync();
        var client = _api.CreateClient(manager);

        var set = await _api.WaitingForMessagesAsync(() => client.SetSpendingCapAsync(tenant, 5_000));

        set.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var usage = await UsageAsync(client, tenant);
        (usage.GetProperty("spendingCapOffered").GetBoolean(), usage.GetProperty("spendingCap").GetProperty("amount").GetInt64(), usage.GetProperty("spendingCap").GetProperty("reachedAt").ValueKind)
            .ShouldBe((true, 5_000L, JsonValueKind.Null));
        (await _audit.TenantLogAsync(tenant)).ShouldBe($"{tenant}:tenant.spending_cap_changed|User|{await _testData.UserIdOfAsync(manager)}|Tenant|{tenant}|Acme Ltd");
        (await _audit.TenantDetailsAsync(tenant)).ShouldBe("""{"currency": "EUR", "newAmount": 5000, "oldAmount": null}""");
    }

    [Fact]
    public async Task SetSpendingCap_None_RemovesItAndRecordsTheOldAmount()
    {
        var (tenant, manager) = await PayingTenantAsync();
        var client = _api.CreateClient(manager);
        await _api.WaitingForMessagesAsync(() => client.SetSpendingCapAsync(tenant, 5_000));

        var removed = await _api.WaitingForMessagesAsync(() => client.SetSpendingCapAsync(tenant, null));

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UsageAsync(client, tenant)).GetProperty("spendingCap").ValueKind.ShouldBe(JsonValueKind.Null);
        (await _audit.TenantDetailsAsync(tenant)).ShouldBe(
            """{"currency": "EUR", "newAmount": 5000, "oldAmount": null}, {"currency": "EUR", "newAmount": null, "oldAmount": 5000}""");
    }

    // The cap it has already changes nothing and records nothing (I1).
    [Fact]
    public async Task SetSpendingCap_TheSameAmountAgain_RecordsNothing()
    {
        var (tenant, manager) = await PayingTenantAsync();
        var client = _api.CreateClient(manager);
        await _api.WaitingForMessagesAsync(() => client.SetSpendingCapAsync(tenant, 5_000));

        var again = await _api.WaitingForMessagesAsync(() => client.SetSpendingCapAsync(tenant, 5_000));

        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _audit.TenantDetailsAsync(tenant)).ShouldBe("""{"currency": "EUR", "newAmount": 5000, "oldAmount": null}""");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_000_000_001)]
    public async Task SetSpendingCap_AnAmountOutOfRange_IsRefused(long amount)
    {
        var (tenant, manager) = await PayingTenantAsync();

        var set = await _api.CreateClient(manager).SetSpendingCapAsync(tenant, amount);

        (set.StatusCode, await BillingRequests.CodeOfAsync(set)).ShouldBe((HttpStatusCode.BadRequest, "usage.spending_cap_invalid"));
    }

    [Fact]
    public async Task SetSpendingCap_ForATenantThatDoesNotPayByCard_IsAConflictAndUsageOffersNone()
    {
        var tenant = await TenantWithMeterAsync();
        var manager = await _testData.AddMemberAsync(tenant, role: "BillingManager");
        var client = _api.CreateClient(manager);

        var set = await client.SetSpendingCapAsync(tenant, 5_000);

        (set.StatusCode, await BillingRequests.CodeOfAsync(set)).ShouldBe((HttpStatusCode.Conflict, "usage.spending_cap_unavailable"));
        var usage = await UsageAsync(client, tenant);
        (usage.GetProperty("spendingCapOffered").GetBoolean(), usage.GetProperty("spendingCap").ValueKind).ShouldBe((false, JsonValueKind.Null));
    }

    [Fact]
    public async Task SetSpendingCap_WhileBillingIsOff_IsAConflict()
    {
        await _api.DisposeAsync();
        _api = Api(null);
        var tenant = await TenantWithMeterAsync();
        var owner = await _testData.AddMemberAsync(tenant, role: "Owner");

        var set = await _api.CreateClient(owner).SetSpendingCapAsync(tenant, 5_000);

        (set.StatusCode, await BillingRequests.CodeOfAsync(set)).ShouldBe((HttpStatusCode.Conflict, "usage.spending_cap_unavailable"));
    }

    [Fact]
    public async Task SetSpendingCap_ByAMember_IsForbidden()
    {
        var (tenant, _) = await PayingTenantAsync();
        var member = await _testData.AddMemberAsync(tenant);

        var set = await _api.CreateClient(member).SetSpendingCapAsync(tenant, 5_000);

        set.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<JsonElement> UsageAsync(HttpClient client, Guid tenantId) =>
        await (await client.GetAsync($"/v1/tenants/{tenantId}/usage", Cancellation)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

    private ApiFactory Api(IReadOnlyDictionary<string, string?>? settings) => new(
        database.ConnectionStringFor(DatabaseRoles.Application),
        settings: settings,
        configureServices: services => services.Replace(ServiceDescriptor.Singleton(MeterCode.Of((Meter, "AI requests the members make.")))));

    private async Task<Guid> TenantWithMeterAsync()
    {
        var planId = await _testData.AddPlanAsync([]);
        await _testData.SetPlanPricesAsync(planId, 900, 1000, 9000, 10000);
        await _testData.SetPlanMeterAsync(planId, Meter, 100, "Charge", unitPrice: 100, unitSize: 1);
        return (await _testData.AddTenantAsync(planId: planId, name: "Acme Ltd")).Id;
    }

    private async Task<(Guid TenantId, string Manager)> PayingTenantAsync()
    {
        var tenant = await TenantWithMeterAsync();
        var owner = await _testData.AddMemberAsync(tenant, role: "Owner");
        await _api.PayAsync(owner, tenant, await database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenant}'"));
        return (tenant, await _testData.AddMemberAsync(tenant, role: "BillingManager"));
    }
}
