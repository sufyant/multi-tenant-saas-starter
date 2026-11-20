using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Usage;

// IUsageMeter.UseAsync: a product's module asks ControlPlane whether a tenant may use a meter, and counts the use (step 40c). The count is the
// tenant's for each meter and calendar month of UTC. A stop meter stops at its quota; a charge meter goes on and counts the extra. A meter
// the tenant's plan has no setting for is not on the plan, so its use stops.
[FixedClock("Nothing these tests run schedules a message; the fixed date puts each use in a known month.")]
public sealed class UseMeterTests(Database database) : IAsyncLifetime
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
    public async Task UseMeter_UnderTheQuota_IsUsedAndCounted()
    {
        var tenant = await TenantWithMeterAsync(quota: 100, overage: "Stop");

        var use = await UseAsync(tenant, 40);

        use.ShouldBe(MeterUse.Used);
        (await UsedAsync(tenant, "2026-11-01")).ShouldBe("40|0");
    }

    [Fact]
    public async Task UseMeter_AStopMeterUpToItsQuota_IsUsed()
    {
        var tenant = await TenantWithMeterAsync(quota: 100, overage: "Stop");
        await UseAsync(tenant, 60);

        var use = await UseAsync(tenant, 40);

        use.ShouldBe(MeterUse.Used);
        (await UsedAsync(tenant, "2026-11-01")).ShouldBe("100|0");
    }

    [Fact]
    public async Task UseMeter_AStopMeterOverItsQuota_IsStoppedAndCountsNothing()
    {
        var tenant = await TenantWithMeterAsync(quota: 100, overage: "Stop");
        await UseAsync(tenant, 90);

        var use = await UseAsync(tenant, 11);

        use.ShouldBe(MeterUse.Stopped);
        (await UsedAsync(tenant, "2026-11-01")).ShouldBe("90|0");
    }

    [Fact]
    public async Task UseMeter_AChargeMeterOverItsQuota_IsUsedAndCountsTheExtra()
    {
        var tenant = await TenantWithMeterAsync(quota: 100, overage: "Charge");
        await UseAsync(tenant, 90);

        var use = await UseAsync(tenant, 30);

        use.ShouldBe(MeterUse.Used);
        (await UsedAsync(tenant, "2026-11-01")).ShouldBe("120|20");
    }

    [Fact]
    public async Task UseMeter_AStopMetersFirstUseOverItsQuota_IsStoppedAndLeavesNoRow()
    {
        var tenant = await TenantWithMeterAsync(quota: 10, overage: "Stop");

        var use = await UseAsync(tenant, 11);

        use.ShouldBe(MeterUse.Stopped);
        (await UsedAsync(tenant, "2026-11-01")).ShouldBeNull();
    }

    // Only an active organisation uses its meters: a suspended one is locked out, and a closed one waits for its purge, which deletes the counts.
    [Theory]
    [InlineData("status = 'Suspended', suspension_reason = 'Unpaid invoices'")]
    [InlineData("status = 'Closed', closed_at = now()")]
    [InlineData("status = 'Purging'")]
    public async Task UseMeter_ATenantThatIsNotActive_IsStoppedAndCountsNothing(string state)
    {
        var tenant = await TenantWithMeterAsync(quota: 100, overage: "Charge");
        await database.ScalarAsSuperuserAsync<object>($"UPDATE control_plane.tenants SET {state} WHERE id = '{tenant}'");

        var use = await UseAsync(tenant, 1);

        use.ShouldBe(MeterUse.Stopped);
        (await UsedAsync(tenant, "2026-11-01")).ShouldBeNull();
    }

    [Fact]
    public async Task UseMeter_AMeterThePlanHasNot_IsStopped()
    {
        var tenant = (await _testData.AddTenantAsync(planId: await _testData.AddPlanAsync(new()))).Id;

        var use = await UseAsync(tenant, 1);

        use.ShouldBe(MeterUse.Stopped);
        (await UsedAsync(tenant, "2026-11-01")).ShouldBeNull();
    }

    [Fact]
    public async Task UseMeter_InANewMonth_StartsAgainFromNothing()
    {
        var tenant = await TenantWithMeterAsync(quota: 100, overage: "Stop");
        await UseAsync(tenant, 100);
        _time.SetUtcNow(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));

        var use = await UseAsync(tenant, 5);

        use.ShouldBe(MeterUse.Used);
        (await UsedAsync(tenant, "2026-12-01")).ShouldBe("5|0");
    }

    [Fact]
    public async Task UseMeter_ManyAtOnce_StopsExactlyAtTheQuota()
    {
        var tenant = await TenantWithMeterAsync(quota: 10, overage: "Stop");

        var uses = await Task.WhenAll(Enumerable.Range(0, 15).Select(_ => UseAsync(tenant, 1)));

        uses.Count(use => use == MeterUse.Used).ShouldBe(10);
        (await UsedAsync(tenant, "2026-11-01")).ShouldBe("10|0");
    }

    private async Task<Guid> TenantWithMeterAsync(long quota, string overage)
    {
        var planId = await _testData.AddPlanAsync(new());
        await _testData.SetPlanMeterAsync(planId, Meter, quota, overage, unitPrice: 100, unitSize: 1000);
        return (await _testData.AddTenantAsync(planId: planId)).Id;
    }

    private async Task<MeterUse> UseAsync(Guid tenantId, long quantity)
    {
        using var scope = _api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUsageMeter>().UseAsync(tenantId, Meter, quantity, Cancellation);
    }

    private Task<string?> UsedAsync(Guid tenantId, string period) =>
        database.ScalarAsSuperuserAsync<string?>(
            $"SELECT used || '|' || extra FROM control_plane.meter_usages WHERE tenant_id = '{tenantId}' AND meter_key = '{Meter}' AND period = '{period}'");
}
