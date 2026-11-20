using Billing.Application.Provider;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Usage.CloseMeterMonth;
using ControlPlane.Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Tenant.Usage;

// The extra of a month reaches Stripe once, when the month ends (E11): the first use of the month that costs extra schedules the month's
// close, and the close adds the month's extra of the meter to the subscription's next invoice, at the price the tenant saw. A tenant that
// does not pay by card is charged nothing: a system admin invoices a Contract's extra (4.37). The meter here: a quota of 100, and 1 € for
// each unit over it.
[FixedClock("The close of a month is scheduled for the month's end, and the test delivers it at that moment.")]
public sealed class UsageChargeTests(Database database) : IAsyncLifetime
{
    private const string Meter = "ai.requests";

    private static readonly DateTimeOffset MonthEnd = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TestData _testData = new(database);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero));

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: BillingRequests.On,
            time: _time,
            configureServices: services => services.Replace(ServiceDescriptor.Singleton(MeterCode.Of((Meter, "AI requests.")))));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task UseMeter_TheFirstExtraOfTheMonth_SchedulesTheMonthsCloseAtItsEnd()
    {
        var tenant = await TenantAsync(pays: true);
        await UseAsync(tenant, 100);

        var first = await UseAsync(tenant, 5);
        var second = await UseAsync(tenant, 5);

        first.Scheduled.Envelopes().Select(envelope => (envelope.Message, envelope.ScheduledTime, envelope.TenantId)).ShouldBe(
            [(new CloseMeterMonthCommand(tenant, Meter, new DateOnly(2026, 11, 1)), MonthEnd, tenant.ToString())]);
        second.Scheduled.Envelopes().ShouldBeEmpty();
    }

    [Fact]
    public async Task CloseMeterMonth_AMonthWithExtra_AddsItToTheSubscriptionsNextInvoice()
    {
        var tenant = await TenantAsync(pays: true);
        await UseAsync(tenant, 105);
        await UseAsync(tenant, 10);

        await CloseAsync(tenant);

        _api.Billing.Extras.ShouldBe([new ExtraCharge($"cus_{tenant:N}", $"sub_{tenant:N}", tenant, Meter, new DateOnly(2026, 11, 1), 15, 1500, "EUR")]);
    }

    [Fact]
    public async Task CloseMeterMonth_ATenantThatDoesNotPayByCard_ChargesNothing()
    {
        var tenant = await TenantAsync(pays: false);
        await UseAsync(tenant, 105);

        await CloseAsync(tenant);

        _api.Billing.Extras.ShouldBeEmpty();
    }

    private async Task<Guid> TenantAsync(bool pays)
    {
        var planId = await _testData.AddPlanAsync([]);
        await _testData.SetPlanPricesAsync(planId, 900, 1000, 9000, 10000);
        await _testData.SetPlanMeterAsync(planId, Meter, 100, "Charge", unitPrice: 100, unitSize: 1);
        var tenant = (await _testData.AddTenantAsync(planId: planId)).Id;
        var owner = await _testData.AddMemberAsync(tenant, role: "Owner");
        if (pays)
        {
            await _api.PayAsync(owner, tenant, planId);
        }

        return tenant;
    }

    private Task<ITrackedSession> UseAsync(Guid tenantId, long quantity) =>
        _api.TrackMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            (await scope.ServiceProvider.GetRequiredService<IUsageMeter>().UseAsync(tenantId, Meter, quantity, Cancellation)).ShouldBe(MeterUse.Used);
        });

    // The close as Wolverine delivers it at the month's end, in the tenant of its envelope.
    private async Task CloseAsync(Guid tenantId)
    {
        _time.SetUtcNow(MonthEnd);
        await _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeForTenantAsync(tenantId.ToString(), new CloseMeterMonthCommand(tenantId, Meter, new DateOnly(2026, 11, 1)), Cancellation);
            return tenantId;
        });
    }
}
