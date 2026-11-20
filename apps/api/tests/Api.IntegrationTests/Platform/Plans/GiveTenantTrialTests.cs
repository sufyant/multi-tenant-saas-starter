using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Tenants.EndTenantTrial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform.Plans;

// POST /v1/platform/tenants/{tenantId}/trial: while billing is on, a system admin gives a tenant a trial of a plan for some days. It starts
// now, needs no card, and returns the tenant to its plan of now at its end (step 40d; 4.29, 4.47). Only a system admin gives a second trial.
public sealed class GiveTenantTrialTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    // The clock starts at the real now, in whole seconds: the trial's end is scheduled by Wolverine's clock, days later, so it never comes
    // during a run on the shared database (section 9, rule 10).
    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time, settings: BillingRequests.On);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // A tenant on Team gets 10 days of Business: the answer has the trial, its end is scheduled, and both logs record it with the plan and
    // the end. The platform's log names the system admin; the tenant's own log says the platform acted (K3).
    [Fact]
    public async Task GiveTenantTrial_ATenantOnTeam_StartsTheTrialSchedulesItsEndAndRecordsIt()
    {
        var tenant = await _testData.AddTenantAsync(name: "Northwind Traders", planId: SeedPlans.Team);
        var adminId = await _testData.AddSystemAdminAsync();
        var admin = _api.CreateClient(adminId, secondFactor: true);
        var endsAt = _time.GetUtcNow() + TimeSpan.FromDays(10);
        HttpResponseMessage? given = null;

        var session = await _api.TrackMessagesAsync(async () => given = await GiveAsync(admin, tenant.Id, SeedPlans.Business, 10));

        given!.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plan = await given.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (plan.GetProperty("plan").GetProperty("id").GetGuid(), Trials.Of(plan.GetProperty("trial")))
            .ShouldBe((SeedPlans.Business, $"{SeedPlans.Business}|{_time.GetUtcNow():O}|{endsAt:O}|{SeedPlans.Team}"));
        session.Scheduled.Envelopes().Select(envelope => (envelope.Message, envelope.ScheduledTime))
            .ShouldBe([(new EndTenantTrialCommand(tenant.Id, endsAt), endsAt)]);
        (await _audit.PlatformLogAsync(tenant.Id))
            .ShouldBe($"tenant.trial_given|User|{await _testData.UserIdOfAsync(adminId)}|Tenant|{tenant.Id}|Northwind Traders");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe($"{tenant.Id}:tenant.trial_given|Platform|-|Tenant|{tenant.Id}|Northwind Traders");
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe(
            $$"""{"plan": {"id": "{{SeedPlans.Business}}", "name": "Business"}, "endsAt": "{{endsAt:yyyy-MM-ddTHH:mm:ssK}}"}""");
    }

    // 4.47: a tenant that used a trial gets another one from a system admin.
    [Fact]
    public async Task GiveTenantTrial_ATenantThatUsedATrial_StartsANewOne()
    {
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);
        await Trials.UsedAsync(database, tenant.Id);

        var given = await _api.WaitingForMessagesAsync(async () => await GiveAsync(await AdminAsync(), tenant.Id, SeedPlans.Business, 14));

        given.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Trials.InTrialAsync(database, tenant.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task GiveTenantTrial_InTrial_IsAConflictAndChangesNothing()
    {
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);
        await Trials.StartAsync(database, tenant.Id, SeedPlans.Team);

        var refused = await GiveAsync(await AdminAsync(), tenant.Id, SeedPlans.Team, 14);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.Conflict, "tenant.in_trial"));
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT concat_ws('|', plan_id, plan_before_trial_id, trial_ends_at = '{Trials.EndsAt:O}') FROM control_plane.tenants WHERE id = '{tenant.Id}'"))
            .ShouldBe($"{SeedPlans.Business}|{SeedPlans.Team}|t");
    }

    // A closed tenant's plan is read-only (E2).
    [Fact]
    public async Task GiveTenantTrial_AClosedTenant_IsAConflict()
    {
        var tenant = await _testData.AddTenantAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow, planId: SeedPlans.Team);

        var refused = await GiveAsync(await AdminAsync(), tenant.Id, SeedPlans.Business, 14);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.Conflict, "tenant.not_active"));
        (await Trials.InTrialAsync(database, tenant.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task GiveTenantTrial_AnUnknownPlan_IsNotFound()
    {
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);

        var refused = await GiveAsync(await AdminAsync(), tenant.Id, Guid.NewGuid(), 14);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.NotFound, "plan.not_found"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task GiveTenantTrial_DaysOutOfRange_IsABadRequest(int days)
    {
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);

        var refused = await GiveAsync(await AdminAsync(), tenant.Id, SeedPlans.Business, days);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.BadRequest, "trial.days_invalid"));
        (await Trials.InTrialAsync(database, tenant.Id)).ShouldBeFalse();
    }

    // A trial exists only while billing is on (the maintainer's decision of 2026-11-20).
    [Fact]
    public async Task GiveTenantTrial_BillingOff_IsNotFound()
    {
        await using var off = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);

        var refused = await GiveAsync(off.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true), tenant.Id, SeedPlans.Business, 14);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.NotFound, "trial.billing_off"));
        (await Trials.InTrialAsync(database, tenant.Id)).ShouldBeFalse();
    }

    private static Task<HttpResponseMessage> GiveAsync(HttpClient admin, Guid tenantId, Guid planId, int days) =>
        admin.PostAsJsonAsync($"/v1/platform/tenants/{tenantId}/trial", new { planId, days }, Cancellation);

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
