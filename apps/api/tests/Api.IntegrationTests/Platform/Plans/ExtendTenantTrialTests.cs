using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Tenants.EndTenantTrial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform.Plans;

// PATCH /v1/platform/tenants/{tenantId}/trial: while billing is on, a system admin moves a running trial's end on (step 40d; 4.28, 4.30).
// The new end is scheduled; the message of the old end then does nothing (K4). It sets a state, so the end the trial has changes nothing (I1).
public sealed class ExtendTenantTrialTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    // The clock starts at the real now, in whole seconds: the trial ends days later, so its end never comes during a run on the shared
    // database (section 9, rule 10).
    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private DateTimeOffset TrialEnd => _time.GetUtcNow() + TimeSpan.FromDays(3);

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time, settings: BillingRequests.On);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // 4.30: 7 extra days move the end 7 days on, the new end is scheduled, and both logs record the old and the new end.
    [Fact]
    public async Task ExtendTenantTrial_InTrial_MovesTheEndSchedulesItAndRecordsIt()
    {
        var tenant = await _testData.AddTenantAsync(name: "Northwind Traders", planId: SeedPlans.Team);
        await Trials.StartAsync(database, tenant.Id, SeedPlans.Team, TrialEnd);
        var adminId = await _testData.AddSystemAdminAsync();
        var newEnd = TrialEnd + TimeSpan.FromDays(7);
        HttpResponseMessage? extended = null;

        var session = await _api.TrackMessagesAsync(async () => extended = await ExtendAsync(_api.CreateClient(adminId, secondFactor: true), tenant.Id, newEnd));

        extended!.StatusCode.ShouldBe(HttpStatusCode.OK);
        Trials.Of((await extended.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("trial"))
            .ShouldBe($"{SeedPlans.Business}|{Trials.StartedAt:O}|{newEnd:O}|{SeedPlans.Team}");
        session.Scheduled.Envelopes().Select(envelope => (envelope.Message, envelope.ScheduledTime))
            .ShouldBe([(new EndTenantTrialCommand(tenant.Id, newEnd), newEnd)]);
        (await _audit.PlatformLogAsync(tenant.Id))
            .ShouldBe($"tenant.trial_extended|User|{await _testData.UserIdOfAsync(adminId)}|Tenant|{tenant.Id}|Northwind Traders");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe($"{tenant.Id}:tenant.trial_extended|Platform|-|Tenant|{tenant.Id}|Northwind Traders");
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe(
            $$"""{"newEndsAt": "{{newEnd:yyyy-MM-ddTHH:mm:ssK}}", "oldEndsAt": "{{TrialEnd:yyyy-MM-ddTHH:mm:ssK}}"}""");
    }

    // The message of the old end comes and does nothing: the trial runs to its new end (K4).
    [Fact]
    public async Task ExtendTenantTrial_TheOldEndComes_KeepsTheTrial()
    {
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);
        await Trials.StartAsync(database, tenant.Id, SeedPlans.Team, TrialEnd);
        (await _api.WaitingForMessagesAsync(async () => await ExtendAsync(await AdminAsync(), tenant.Id, TrialEnd + TimeSpan.FromDays(7)))).EnsureSuccessStatusCode();

        await DeliverAsync(tenant.Id, new EndTenantTrialCommand(tenant.Id, TrialEnd));

        (await Trials.InTrialAsync(database, tenant.Id)).ShouldBeTrue();
    }

    // The same new end again sets the state the trial has: it records once (I1).
    [Fact]
    public async Task ExtendTenantTrial_TheSameEndTwice_RecordsOnce()
    {
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);
        await Trials.StartAsync(database, tenant.Id, SeedPlans.Team, TrialEnd);
        var admin = await AdminAsync();
        var newEnd = TrialEnd + TimeSpan.FromDays(7);
        (await _api.WaitingForMessagesAsync(() => ExtendAsync(admin, tenant.Id, newEnd))).EnsureSuccessStatusCode();

        var again = await _api.WaitingForMessagesAsync(() => ExtendAsync(admin, tenant.Id, newEnd));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe($"{tenant.Id}:tenant.trial_extended|Platform|-|Tenant|{tenant.Id}|Acme Ltd");
    }

    [Fact]
    public async Task ExtendTenantTrial_NotInTrial_IsAConflict()
    {
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);
        await Trials.UsedAsync(database, tenant.Id);

        var refused = await ExtendAsync(await AdminAsync(), tenant.Id, TrialEnd);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.Conflict, "tenant.not_in_trial"));
    }

    // An end before the trial's end, or more than 365 days after it.
    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public async Task ExtendTenantTrial_AnEarlierOrAFarEnd_IsABadRequest(int days)
    {
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);
        await Trials.StartAsync(database, tenant.Id, SeedPlans.Team, TrialEnd);

        var refused = await ExtendAsync(await AdminAsync(), tenant.Id, TrialEnd + TimeSpan.FromDays(days));

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.BadRequest, "trial.end_invalid"));
        (await database.ScalarAsSuperuserAsync<bool>(
            $"SELECT trial_ends_at = '{TrialEnd:O}' FROM control_plane.tenants WHERE id = '{tenant.Id}'")).ShouldBeTrue();
    }

    // A trial exists only while billing is on (the maintainer's decision of 2026-11-20).
    [Fact]
    public async Task ExtendTenantTrial_BillingOff_IsNotFound()
    {
        await using var off = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Team);
        await Trials.StartAsync(database, tenant.Id, SeedPlans.Team, TrialEnd);

        var refused = await ExtendAsync(off.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true), tenant.Id, TrialEnd + TimeSpan.FromDays(7));

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.NotFound, "trial.billing_off"));
    }

    private static Task<HttpResponseMessage> ExtendAsync(HttpClient admin, Guid tenantId, DateTimeOffset endsAt) =>
        admin.PatchAsJsonAsync($"/v1/platform/tenants/{tenantId}/trial", new { endsAt }, Cancellation);

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    // Delivers the message now, in the tenant, as Wolverine delivers a scheduled message when its moment comes.
    private Task<Guid> DeliverAsync(Guid tenantId, object message) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), message, Cancellation);
            return tenantId;
        });

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
