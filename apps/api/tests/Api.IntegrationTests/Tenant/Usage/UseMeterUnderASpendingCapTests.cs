using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Usage;

// A spending cap stops the extra of every charge meter of the tenant together, this calendar month, when the extra's amount reaches it
// (11.02, 11.04; E9). A use that lands on the cap is used, and one that would take the extra over it is stopped; either reaches the cap,
// which stops every later extra of the month until the cap is raised. The meter here: a quota of 100, and 1 € for each unit over it.
public sealed class UseMeterUnderASpendingCapTests(Database database) : IAsyncLifetime
{
    private const string Meter = "ai.requests";

    private const string Images = "ai.images";

    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: BillingRequests.On,
            configureServices: services => services.Replace(ServiceDescriptor.Singleton(MeterCode.Of((Meter, "AI requests."), (Images, "AI images.")))));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task UseMeter_UpToTheCap_IsUsedAndReachesIt()
    {
        var (tenant, manager) = await CappedTenantAsync(cap: 500);

        var uses = new[] { await UseAsync(tenant, Meter, 103), await UseAsync(tenant, Meter, 2), await UseAsync(tenant, Meter, 1) };

        uses.ShouldBe([MeterUse.Used, MeterUse.Used, MeterUse.Stopped]);
        (await UsedAsync(tenant, Meter)).ShouldBe("105|5");
        (await CapAsync(manager, tenant)).GetProperty("reachedAt").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task UseMeter_OverTheCap_IsStoppedReachesItAndStopsTheRest()
    {
        var (tenant, manager) = await CappedTenantAsync(cap: 500);
        await UseAsync(tenant, Meter, 104);

        var uses = new[] { await UseAsync(tenant, Meter, 2), await UseAsync(tenant, Meter, 1) };

        uses.ShouldBe([MeterUse.Stopped, MeterUse.Stopped]);
        (await UsedAsync(tenant, Meter)).ShouldBe("104|4");
        (await CapAsync(manager, tenant)).GetProperty("reachedAt").ValueKind.ShouldBe(JsonValueKind.String);
    }

    // The cap counts the extra of every charge meter of the month together.
    [Fact]
    public async Task UseMeter_TheExtraOfAnotherMeter_CountsTowardsTheCap()
    {
        var (tenant, _) = await CappedTenantAsync(cap: 500);
        await UseAsync(tenant, Images, 104);

        var use = await UseAsync(tenant, Meter, 102);

        use.ShouldBe(MeterUse.Stopped);
    }

    // A use that adds no extra is the plan's, which the cap does not touch (11.03).
    [Fact]
    public async Task UseMeter_UnderItsQuotaAfterTheCap_IsUsed()
    {
        var (tenant, _) = await CappedTenantAsync(cap: 500);
        await UseAsync(tenant, Images, 105);

        var use = await UseAsync(tenant, Meter, 100);

        use.ShouldBe(MeterUse.Used);
    }

    [Fact]
    public async Task UseMeter_AfterAHigherCap_IsUsedAgain()
    {
        var (tenant, manager) = await CappedTenantAsync(cap: 500);
        await UseAsync(tenant, Meter, 105);
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).SetSpendingCapAsync(tenant, 1_000));

        var use = await UseAsync(tenant, Meter, 1);

        use.ShouldBe(MeterUse.Used);
        (await CapAsync(manager, tenant)).GetProperty("reachedAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // A tenant that no longer pays by card has no cap, as Contract and billing off charge no extra in the app (E9).
    [Fact]
    public async Task UseMeter_WithACapLeftFromPayingByCard_IsNotStopped()
    {
        var tenant = await TenantWithMetersAsync();
        await database.ScalarAsSuperuserAsync<object>(
            $"INSERT INTO control_plane.spending_caps (tenant_id, amount) VALUES ('{tenant}', 500)");

        var use = await UseAsync(tenant, Meter, 200);

        use.ShouldBe(MeterUse.Used);
    }

    private async Task<Guid> TenantWithMetersAsync()
    {
        var planId = await _testData.AddPlanAsync([]);
        await _testData.SetPlanPricesAsync(planId, 900, 1000, 9000, 10000);
        await _testData.SetPlanMeterAsync(planId, Meter, 100, "Charge", unitPrice: 100, unitSize: 1);
        await _testData.SetPlanMeterAsync(planId, Images, 100, "Charge", unitPrice: 100, unitSize: 1);
        return (await _testData.AddTenantAsync(planId: planId)).Id;
    }

    private async Task<(Guid TenantId, string Manager)> CappedTenantAsync(long cap)
    {
        var tenant = await TenantWithMetersAsync();
        var owner = await _testData.AddMemberAsync(tenant, role: "Owner");
        await _api.PayAsync(owner, tenant, await database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenant}'"));
        var manager = await _testData.AddMemberAsync(tenant, role: "BillingManager");
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).SetSpendingCapAsync(tenant, cap));
        return (tenant, manager);
    }

    private async Task<MeterUse> UseAsync(Guid tenantId, string meter, long quantity)
    {
        using var scope = _api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUsageMeter>().UseAsync(tenantId, meter, quantity, Cancellation);
    }

    private Task<string?> UsedAsync(Guid tenantId, string meter) =>
        database.ScalarAsSuperuserAsync<string?>(
            $"SELECT used || '|' || extra FROM control_plane.meter_usages WHERE tenant_id = '{tenantId}' AND meter_key = '{meter}'");

    private async Task<JsonElement> CapAsync(string manager, Guid tenantId) =>
        (await (await _api.CreateClient(manager).GetAsync($"/v1/tenants/{tenantId}/usage", Cancellation)).Content.ReadFromJsonAsync<JsonElement>(Cancellation))
            .GetProperty("spendingCap");
}
