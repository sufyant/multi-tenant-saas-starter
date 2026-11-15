using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Subscriptions;

// DELETE /v1/tenants/{tenantId}/billing/scheduled-change (B9, 10.07): the undo of a move or a cancellation that a member scheduled at the
// period's end. With nothing scheduled, it changes nothing and calls no provider.
public sealed class UndoScheduledChangeTests(Database database) : IAsyncLifetime
{
    private ApiFactory _api = null!;

    private PayingTenants _tenants = null!;

    private readonly AuditEntries _audit = new(database);

    private readonly TestData _testData = new(database);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: BillingRequests.On);
        _tenants = new PayingTenants(database, _api);
        await _tenants.AddPlansAsync();
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task UndoScheduledChange_AfterACancellation_KeepsThePlanAndItsPayments()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var free = await _tenants.DefaultPlanAsync();
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, free));

        var answer = await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).UndoScheduledChangeAsync(tenantId));

        answer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _api.Billing.Undone.ShouldBe([$"sub_{tenantId:N}"]);
        var subscription = (await (await _api.CreateClient(manager).GetBillingAsync(tenantId)).Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("subscription");
        subscription.GetProperty("cancelAtPeriodEnd").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task UndoScheduledChange_AfterAScheduledMove_DropsTheMove()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Business);
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Team));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).UndoScheduledChangeAsync(tenantId));

        (await _tenants.SubscriptionOfAsync(tenantId)).ShouldBe($"{_tenants.Business}|Monthly|f||");
    }

    // 12.12: each undo goes to both logs, by the member who undid it (B9).
    [Fact]
    public async Task UndoScheduledChange_AfterACancellation_RecordsTheUndoByTheMember()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var member = await _testData.UserIdOfAsync(manager);
        var free = await _tenants.DefaultPlanAsync();
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, free));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).UndoScheduledChangeAsync(tenantId));

        (await _audit.PlatformLogAsync(tenantId))!.Split(", ").ShouldContain($"tenant.plan_cancellation_undone|User|{member}|Tenant|{tenantId}|Acme Ltd");
    }

    [Fact]
    public async Task UndoScheduledChange_AfterAScheduledMove_RecordsTheUndoByTheMember()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Business);
        var member = await _testData.UserIdOfAsync(manager);
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Team));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).UndoScheduledChangeAsync(tenantId));

        (await _audit.TenantLogAsync(tenantId))!.Split(", ").ShouldContain($"{tenantId}:tenant.plan_change_undone|User|{member}|Tenant|{tenantId}|Acme Ltd");
    }

    [Fact]
    public async Task UndoScheduledChange_NothingScheduled_IsAConflict()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);

        var answer = await _api.CreateClient(manager).UndoScheduledChangeAsync(tenantId);

        answer.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.nothing_scheduled");
        _api.Billing.Undone.ShouldBeEmpty();
    }
}
