using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Subscriptions;

// POST /v1/tenants/{tenantId}/billing/plan-change (B9, 10.02 to 10.07). A member who manages billing moves the tenant's subscription: an
// upgrade and monthly to yearly at once with proration, a cheaper plan and yearly to monthly at the period's end, and the default plan as
// the cancellation at the period's end. The tenant's plan changes when the provider's state is synced, as after a checkout (B6), and both
// audit logs name the member who changed it.
public sealed class ChangePlanTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

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
    public async Task ChangePlan_ToAMoreExpensivePlan_MovesAtOnceAndNamesTheMemberWhoChangedIt()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var changer = await _testData.UserIdOfAsync(manager);

        var answer = await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Business));

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await MoveOfAsync(answer)).ShouldBe("at_once");
        _api.Billing.MovesNow.Select(move => (move.Order.SubscriptionId, move.Order.PlanId, move.Order.BaseAmount, move.Order.PerPersonAmount, move.Order.People, move.Order.Yearly))
            .ShouldBe([($"sub_{tenantId:N}", _tenants.Business, 9900L, 800L, 1, false)]);
        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Business);
        (await _audit.TenantLogAsync(tenantId))!.Split(", ").ShouldContain($"{tenantId}:tenant.plan_changed|User|{changer}|Tenant|{tenantId}|Acme Ltd");
    }

    [Fact]
    public async Task ChangePlan_MonthlyToYearly_MovesAtOnce()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);

        var answer = await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Team, "yearly"));

        (await MoveOfAsync(answer)).ShouldBe("at_once");
        (await _tenants.SubscriptionOfAsync(tenantId)).ShouldBe($"{_tenants.Team}|Yearly|f||");
    }

    [Fact]
    public async Task ChangePlan_ADeclinedCard_IsUnprocessableAndKeepsThePlan()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        _api.Billing.Declines = true;

        var answer = await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Business));

        answer.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.payment_declined");
        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Team);
    }

    [Fact]
    public async Task ChangePlan_ToACheaperPlan_SchedulesItAtThePeriodsEnd()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Business);

        var answer = await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Team));

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var move = await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (move.GetProperty("move").GetString(), move.GetProperty("at").GetDateTimeOffset()).ShouldBe(("at_period_end", PayingTenants.PeriodEnd));
        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Business);
        var scheduled = (await BillingOfAsync(manager, tenantId)).GetProperty("scheduledChange");
        (scheduled.GetProperty("planId").GetGuid(), scheduled.GetProperty("interval").GetString()).ShouldBe((_tenants.Team, "monthly"));
    }

    [Fact]
    public async Task ChangePlan_YearlyToMonthly_SchedulesItAtTheEndOfTheYear()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Team, "yearly"));

        var answer = await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Business));

        (await MoveOfAsync(answer)).ShouldBe("at_period_end");
        (await _tenants.SubscriptionOfAsync(tenantId)).ShouldBe($"{_tenants.Team}|Yearly|f|{_tenants.Business}|Monthly");
    }

    // 12.12: the member's cancellation goes to both logs, by them, with the plan it moves to and the day it moves (B9).
    [Fact]
    public async Task ChangePlan_ToTheDefaultPlan_RecordsTheCancellationByTheMember()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var changer = await _testData.UserIdOfAsync(manager);
        var free = await _tenants.DefaultPlanAsync();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, free));

        (await _audit.TenantLogAsync(tenantId))!.Split(", ").ShouldContain($"{tenantId}:tenant.plan_cancelled|User|{changer}|Tenant|{tenantId}|Acme Ltd");
        (await _audit.PlatformLogAsync(tenantId))!.Split(", ").ShouldContain($"tenant.plan_cancelled|User|{changer}|Tenant|{tenantId}|Acme Ltd");
        var details = JsonDocument.Parse((await database.ScalarAsSuperuserAsync<string>(
            $"SELECT details::text FROM audit.entries WHERE target_id = '{tenantId}' AND operation = 'tenant.plan_cancelled'"))!).RootElement;
        (details.GetProperty("plan").GetProperty("id").GetGuid(), details.GetProperty("newPlan").GetProperty("id").GetGuid(), details.GetProperty("movesAt").GetDateTimeOffset())
            .ShouldBe((_tenants.Team, free, PayingTenants.PeriodEnd));
    }

    [Fact]
    public async Task ChangePlan_ToACheaperPlan_RecordsTheScheduledMoveByTheMember()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Business);
        var changer = await _testData.UserIdOfAsync(manager);

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Team));

        (await _audit.TenantLogAsync(tenantId))!.Split(", ").ShouldContain($"{tenantId}:tenant.plan_change_scheduled|User|{changer}|Tenant|{tenantId}|Acme Ltd");
    }

    // B8, B9: the member's cancellation is no closure's, so a reopen never takes it back.
    [Fact]
    public async Task ChangePlan_ToTheDefaultPlan_CancelsAtThePeriodsEndAsTheMembersWish()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var free = await _tenants.DefaultPlanAsync();

        var answer = await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, free));

        (await MoveOfAsync(answer)).ShouldBe("cancellation");
        _api.Billing.PeriodEnds.ShouldBe([($"sub_{tenantId:N}", true)]);
        (await BillingOfAsync(manager, tenantId)).GetProperty("cancelAtPeriodEnd").GetBoolean().ShouldBeTrue();
        (await database.ScalarAsSuperuserAsync<string>($"SELECT closure_end FROM billing.subscriptions WHERE tenant_id = '{tenantId}'")).ShouldBe("None");
        (await _tenants.PlanOfAsync(tenantId)).ShouldBe(_tenants.Team);
    }

    [Fact]
    public async Task ChangePlan_ThePlanItPaysFor_IsAConflict()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);

        var answer = await _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Team);

        answer.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.plan_unchanged");
    }

    [Fact]
    public async Task ChangePlan_BeforeTheFirstPayment_IsAConflict()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var answer = await _api.CreateClient(owner).ChangePlanAsync(tenant.Id, _tenants.Business);

        answer.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.no_subscription");
    }

    [Fact]
    public async Task ChangePlan_ATenantThatBillingKnowsClosed_IsAConflictAndCallsNoProvider()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        await database.ScalarAsSuperuserAsync<object>($"UPDATE billing.customers SET closed_at = now() WHERE tenant_id = '{tenantId}'");

        var answer = await _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Business);

        answer.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.tenant_closed");
        _api.Billing.MovesNow.ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangePlan_APreviewOlderThanHalfAnHour_IsRefused()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);

        var answer = await _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Business, proratedAt: DateTimeOffset.UtcNow.AddHours(-1));

        answer.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.preview_expired");
        _api.Billing.MovesNow.ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangePlan_WithItsPreviewsMoment_ProratesAtThatMoment()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);
        var preview = await (await _api.CreateClient(manager).PreviewPlanChangeAsync(tenantId, _tenants.Business)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var proratedAt = preview.GetProperty("proratedAt").GetDateTimeOffset();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).ChangePlanAsync(tenantId, _tenants.Business, proratedAt: proratedAt));

        _api.Billing.MovesNow.Select(move => move.ProratedAt).ShouldBe([proratedAt]);
    }

    [Fact]
    public async Task ChangePlan_ByAMember_IsForbidden()
    {
        var (tenantId, _) = await _tenants.PayingAsync(_tenants.Team);
        var member = await _testData.AddMemberAsync(tenantId);

        var answer = await _api.CreateClient(member).ChangePlanAsync(tenantId, _tenants.Business);

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangePlan_BillingOff_IsNotFound()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await using var off = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));

        var answer = await off.CreateClient(owner).ChangePlanAsync(tenant.Id, _tenants.Business);

        answer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BillingRequests.CodeOfAsync(answer)).ShouldBe("billing.off");
    }

    private static async Task<string?> MoveOfAsync(HttpResponseMessage answer) =>
        (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("move").GetString();

    private async Task<JsonElement> BillingOfAsync(string member, Guid tenantId) =>
        (await (await _api.CreateClient(member).GetBillingAsync(tenantId)).Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("subscription");
}
