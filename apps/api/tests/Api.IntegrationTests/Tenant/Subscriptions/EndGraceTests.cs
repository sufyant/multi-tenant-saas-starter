using System.Net.Http.Json;
using System.Text.Json;
using Billing.Application.Subscriptions.EndGrace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Tenant.Subscriptions;

// B11, 10.16 and 10.17: billing's grace of an unpaid subscription ends a week after Stripe's retries, and the tenant moves to the default
// plan, by nobody, in both logs; nothing is deleted. A payment of the open invoice then puts the tenant back on its plan. The module's clock
// decides whether the grace is over, so the test moves it and delivers the scheduled message, as Wolverine does when its moment comes.
public sealed class EndGraceTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    private ApiFactory _api = null!;

    private PayingTenants _tenants = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time, settings: BillingRequests.On);
        _tenants = new PayingTenants(database, _api);
        await _tenants.AddPlansAsync();
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task EndGrace_AtItsEnd_MovesTheTenantToTheDefaultPlanAndRecordsItInBothLogs()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var end = await GraceAsync(tenantId);
        _time.Advance(TimeSpan.FromDays(7));

        await DeliverAsync(new EndGraceCommand(tenantId, end));

        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(await _tenants.DefaultPlanAsync());
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = 'tenant.moved_to_default_plan' AND actor_kind = 'Platform'")).ShouldBe(1);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}' AND operation = 'tenant.moved_to_default_plan'")).ShouldBe(1);
        var payment = await PaymentAsync(manager, tenantId);
        (payment.GetProperty("state").GetString(), payment.GetProperty("movedAt").GetDateTimeOffset()).ShouldBe(("moved_to_default", _time.GetUtcNow()));
    }

    // A message that arrives early moves nothing: the grace still runs.
    [Fact]
    public async Task EndGrace_BeforeItsEnd_MovesNothing()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var end = await GraceAsync(tenantId);

        await DeliverAsync(new EndGraceCommand(tenantId, end));

        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Team);
        var payment = await PaymentAsync(manager, tenantId);
        (payment.GetProperty("state").GetString(), payment.GetProperty("graceEndsAt").GetDateTimeOffset()).ShouldBe(("grace", end));
    }

    // 10.17: "Pay the open invoice to get {plan} back at once."
    [Fact]
    public async Task EndGrace_ThenAPaymentOfTheOpenInvoice_PutsTheTenantBackOnItsPlan()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var end = await GraceAsync(tenantId);
        _time.Advance(TimeSpan.FromDays(7));
        await DeliverAsync(new EndGraceCommand(tenantId, end));
        _api.Billing.Put(BillingRequests.Subscription(tenantId, _tenants.Team));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "invoice.paid", $"sub_{tenantId:N}"));

        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Team);
        var subscription = (await (await _api.CreateClient(manager).GetBillingAsync(tenantId)).Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("subscription");
        subscription.GetProperty("payment").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // E2: a closed tenant's plan is read-only, so its grace moves nothing; its subscription ends at its period's end (B8).
    [Fact]
    public async Task EndGrace_OfAClosedTenant_MovesNothing()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var end = await GraceAsync(tenantId);
        await database.ScalarAsSuperuserAsync<object>($"UPDATE billing.customers SET closed_at = now() WHERE tenant_id = '{tenantId}'");
        _time.Advance(TimeSpan.FromDays(7));

        await DeliverAsync(new EndGraceCommand(tenantId, end));

        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Team);
        (await PaymentAsync(manager, tenantId)).GetProperty("state").GetString().ShouldBe("grace");
    }

    // B12: a grace that started before the tenant became Contract moves nothing at its end: a system admin sets a Contract tenant's plan.
    [Fact]
    public async Task EndGrace_OfATenantMarkedContractDuringIt_MovesNothing()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var end = await GraceAsync(tenantId);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));
        _time.Advance(TimeSpan.FromDays(7));

        await DeliverAsync(new EndGraceCommand(tenantId, end));

        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Team);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = 'tenant.moved_to_default_plan'")).ShouldBe(0);
    }

    // Stripe's retries of the tenant's invoice are over: billing's grace starts, and the test reads the end it scheduled.
    private async Task<DateTimeOffset> GraceAsync(Guid tenantId)
    {
        _api.Billing.Put(BillingRequests.PastDue(tenantId, _tenants.Team, _time.GetUtcNow().AddDays(-14), 4, null));
        var session = await _api.TrackMessagesAsync(() => _api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "invoice.payment_failed", $"sub_{tenantId:N}"));
        return session.Scheduled.MessagesOf<EndGraceCommand>().Single().GraceEndsAt;
    }

    private Task<EndGraceCommand> DeliverAsync(EndGraceCommand command) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(command.TenantId.ToString(), command, Cancellation);
            return command;
        });

    private async Task<JsonElement> PaymentAsync(string member, Guid tenantId) =>
        (await (await _api.CreateClient(member).GetBillingAsync(tenantId)).Content.ReadFromJsonAsync<JsonElement>(Cancellation))
            .GetProperty("subscription").GetProperty("payment");
}
