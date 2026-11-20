using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Usage;

// GET /v1/tenants/{tenantId}/usage (11.01 to 11.06): this calendar month's use
// of each meter of the tenant's plan, with the plan's setting, and the three months before it from the month the tenant was created.
[FixedClock("Nothing these tests run schedules a message; the fixed date puts each use in a known month.")]
public sealed class GetUsageTests(Database database) : IAsyncLifetime
{
    private const string Meter = "ai.requests";

    private readonly TestData _testData = new(database);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero));

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            time: _time,
            configureServices: services => services.Replace(ServiceDescriptor.Singleton(MeterCode.Of((Meter, "AI requests the members make.")))));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task GetUsage_ForABillingManager_AnswersThisMonthsUseOfEachMeterOfThePlan()
    {
        var (tenant, plan) = await TenantWithMeterAsync(new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero));
        await UseAsync(tenant, 10_000);
        _time.Advance(TimeSpan.FromHours(1));
        await UseAsync(tenant, 1_400);
        var manager = await _testData.AddMemberAsync(tenant, role: "BillingManager");

        var answer = await _api.CreateClient(manager).GetAsync($"/v1/tenants/{tenant}/usage", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var usage = await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (usage.GetProperty("periodStart").GetString(), usage.GetProperty("periodEnd").GetString(), usage.GetProperty("planName").GetString(), usage.GetProperty("currency").GetString())
            .ShouldBe(("2026-11-01", "2026-11-30", plan, "EUR"));
        var meter = usage.GetProperty("meters").EnumerateArray().Single();
        $"{meter.GetProperty("key")}|{meter.GetProperty("quota")}|{meter.GetProperty("overage")}|{meter.GetProperty("unitPrice")}|{meter.GetProperty("unitSize")}|{meter.GetProperty("used")}|{meter.GetProperty("extra")}|{meter.GetProperty("extraAmount")}|{meter.GetProperty("quotaReachedAt")}"
            .ShouldBe("ai.requests|10000|charge|100|1000|11400|1400|140|2026-11-18T09:00:00+00:00");
    }

    [Fact]
    public async Task GetUsage_NothingUsedInTheTenantsFirstMonth_AnswersZeroAndNoPastMonth()
    {
        var (tenant, _) = await TenantWithMeterAsync(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
        var owner = await _testData.AddMemberAsync(tenant, role: "Owner");

        var usage = await (await _api.CreateClient(owner).GetAsync($"/v1/tenants/{tenant}/usage", Cancellation)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        var meter = usage.GetProperty("meters").EnumerateArray().Single();
        (meter.GetProperty("used").GetInt64(), meter.GetProperty("extra").GetInt64(), meter.GetProperty("quotaReachedAt").ValueKind).ShouldBe((0L, 0L, JsonValueKind.Null));
        usage.GetProperty("pastPeriods").GetArrayLength().ShouldBe(0);
    }

    // A month without a use answers 0. Its price is the one of its last use: a past month says what its extra cost then.
    [Fact]
    public async Task GetUsage_PastMonths_AnswersTheThreeBeforeThisOneWithTheirExtraAtThePriceOfThen()
    {
        var (tenant, _) = await TenantWithMeterAsync(new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero));
        await AddPastUseAsync(tenant, "2026-10-01", used: 9_100, extra: 0, unitPrice: 100);
        await AddPastUseAsync(tenant, "2026-08-01", used: 10_600, extra: 600, unitPrice: 200);
        await AddPastUseAsync(tenant, "2026-07-01", used: 5, extra: 0, unitPrice: 100);
        var owner = await _testData.AddMemberAsync(tenant, role: "Owner");

        var usage = await (await _api.CreateClient(owner).GetAsync($"/v1/tenants/{tenant}/usage", Cancellation)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        usage.GetProperty("pastPeriods").EnumerateArray()
            .Select(period => $"{period.GetProperty("periodStart")} " + string.Join(",", period.GetProperty("meters").EnumerateArray().Select(m => $"{m.GetProperty("key")}:{m.GetProperty("used")}:{m.GetProperty("extra")}:{m.GetProperty("extraAmount")}")))
            .ShouldBe(["2026-10-01 ai.requests:9100:0:0", "2026-09-01 ai.requests:0:0:0", "2026-08-01 ai.requests:10600:600:120"]);
    }

    [Fact]
    public async Task GetUsage_ByAMember_IsForbidden()
    {
        var (tenant, _) = await TenantWithMeterAsync(new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero));
        var member = await _testData.AddMemberAsync(tenant);

        var answer = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant}/usage", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<(Guid TenantId, string PlanName)> TenantWithMeterAsync(DateTimeOffset createdAt)
    {
        var name = $"Team {Guid.NewGuid():N}";
        var planId = await _testData.AddPlanAsync(new(), name: name);
        await _testData.SetPlanMeterAsync(planId, Meter, 10_000, "Charge", unitPrice: 100, unitSize: 1000);
        return ((await _testData.AddTenantAsync(planId: planId, createdAt: createdAt)).Id, name);
    }

    private async Task UseAsync(Guid tenantId, long quantity)
    {
        using var scope = _api.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IUsageMeter>().UseAsync(tenantId, Meter, quantity, Cancellation))
            .ShouldBe(MeterUse.Used);
    }

    private Task<object?> AddPastUseAsync(Guid tenantId, string period, long used, long extra, long unitPrice) =>
        database.ScalarAsSuperuserAsync<object>(
            $"INSERT INTO control_plane.meter_usages (tenant_id, meter_key, period, used, extra, unit_price, unit_size) VALUES ('{tenantId}', '{Meter}', '{period}', {used}, {extra}, {unitPrice}, 1000)");
}
