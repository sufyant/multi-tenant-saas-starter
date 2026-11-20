using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Tenants.EndTenantTrial;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Me.Tenants;

// Step 40d: while billing is on, a user's own organisation starts a 14-day trial of the trial plan, Business (1.28, 10.08). The trial ends
// at its end by a scheduled message, which returns the tenant to the plan it had before the trial (the maintainer's decision of
// 2026-11-20); the first payment ends it earlier (10.08: "Paying ends the trial.").
public sealed class TenantTrialTests(Database database) : IAsyncLifetime
{
    private static readonly TimeSpan TrialLength = TimeSpan.FromDays(14);

    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    // The clock starts at the real now: the trial's end is scheduled 14 days after it, by Wolverine's clock, so it never comes during a run
    // on the shared database (section 9, rule 10).
    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            time: _time,
            settings: new Dictionary<string, string?>(SignUpSettings.On.Concat(BillingRequests.On)));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CreateMyTenant_BillingOn_StartsATrialOfBusinessAndSchedulesItsEnd()
    {
        var person = await NewUserAsync();
        var now = _time.GetUtcNow();

        var (tenantId, scheduled) = await CreateTrackedAsync(person);

        (await TrialOfAsync(tenantId)).ShouldBe($"{SeedPlans.Business}|{SeedPlans.Business}|{await DefaultPlanAsync()}|{now.UtcTicks}|{(now + TrialLength).UtcTicks}");
        scheduled.ShouldBe([(new EndTenantTrialCommand(tenantId, now + TrialLength), now + TrialLength)]);
    }

    // 4.28, 10.08: both apps read the trial, its plan, its dates and the plan the tenant returns to.
    [Fact]
    public async Task GetMyTenant_InTrial_AnswersTheTrial()
    {
        var person = await NewUserAsync();
        var now = _time.GetUtcNow();
        var (tenantId, _) = await CreateTrackedAsync(person);

        var tenant = await _api.CreateClient(person).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenantId}", Cancellation);
        var platform = await _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true)
            .GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenantId}/plan", Cancellation);

        var expected = $"{SeedPlans.Business}|{now:O}|{now + TrialLength:O}|{await DefaultPlanAsync()}";
        (Trial(tenant.GetProperty("trial")), Trial(platform.GetProperty("trial"))).ShouldBe((expected, expected));
    }

    [Fact]
    public async Task GetMyTenant_NoTrial_AnswersNone()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var read = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}", Cancellation);

        read.GetProperty("trial").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // The trial's end returns the tenant to its plan before; the platform acted, in both logs.
    [Fact]
    public async Task EndTenantTrial_AtItsEnd_ReturnsTheTenantToThePlanBeforeAndRecordsIt()
    {
        var (tenantId, scheduled) = await CreateTrackedAsync(await NewUserAsync());
        _time.Advance(TrialLength);

        await DeliverAsync(tenantId, scheduled.Single().Message);

        (await PlanOfAsync(tenantId), await InTrialAsync(tenantId)).ShouldBe((await DefaultPlanAsync(), false));
        (await _audit.TenantLogAsync(tenantId))!.ShouldEndWith($"{tenantId}:tenant.trial_ended|Platform|-|Tenant|{tenantId}|Kestrel Analytics");
        (await _audit.PlatformLogAsync(tenantId))!.ShouldEndWith($"tenant.trial_ended|Platform|-|Tenant|{tenantId}|Kestrel Analytics");
    }

    // K4's pattern: nothing cancels a scheduled message, so a message of another end, as an extension leaves behind, does nothing.
    [Fact]
    public async Task EndTenantTrial_OfAnotherEnd_ChangesNothing()
    {
        var (tenantId, _) = await CreateTrackedAsync(await NewUserAsync());

        await DeliverAsync(tenantId, new EndTenantTrialCommand(tenantId, _time.GetUtcNow() + TimeSpan.FromDays(3)));

        (await PlanOfAsync(tenantId), await InTrialAsync(tenantId)).ShouldBe((SeedPlans.Business, true));
    }

    // 10.08: the first payment ends the trial, and the tenant stays on the plan it paid for.
    [Fact]
    public async Task PayForPlan_InTrial_EndsTheTrialAndPutsTheTenantOnThePaidPlan()
    {
        var person = await NewUserAsync();
        var (tenantId, _) = await CreateTrackedAsync(person);
        var team = await _testData.AddPlanAsync(new() { ["members.max"] = TestData.Unlimited }, name: $"Team {Guid.NewGuid():N}");
        await _testData.SetPlanPricesAsync(team, 900, 1000, 9000, 10000);
        _time.Advance(TimeSpan.FromDays(2));

        (await _api.PayAsync(person, tenantId, team)).EnsureSuccessStatusCode();

        (await PlanOfAsync(tenantId), await InTrialAsync(tenantId)).ShouldBe((team, false));
        (await database.ScalarAsSuperuserAsync<bool>(
            $"SELECT trial_ends_at = '{_time.GetUtcNow():O}'::timestamptz FROM control_plane.tenants WHERE id = '{tenantId}'")).ShouldBeTrue();
    }

    private static string Trial(JsonElement trial) =>
        string.Join(
            '|',
            trial.GetProperty("plan").GetProperty("id").GetGuid(),
            trial.GetProperty("startedAt").GetDateTimeOffset().ToString("O"),
            trial.GetProperty("endsAt").GetDateTimeOffset().ToString("O"),
            trial.GetProperty("returnsTo").GetProperty("id").GetGuid());

    private Task<string> NewUserAsync() => _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));

    // POST /v1/me/tenants, with the messages it scheduled and their moments.
    private async Task<(Guid TenantId, List<(object Message, DateTimeOffset? At)> Scheduled)> CreateTrackedAsync(string person)
    {
        HttpResponseMessage? created = null;
        var session = await _api.TrackMessagesAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/me/tenants")
            {
                Content = JsonContent.Create(new { name = "Kestrel Analytics", slug = $"kestrel-{Guid.NewGuid():N}"[..20] }),
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            created = await _api.CreateClient(person).SendAsync(request, Cancellation);
        });
        created!.EnsureSuccessStatusCode();
        var tenantId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        return (tenantId, [.. session.Scheduled.Envelopes().Select(envelope => (envelope.Message!, envelope.ScheduledTime))]);
    }

    // Delivers the message now, in the tenant, as Wolverine delivers a scheduled message when its moment comes.
    private Task<Guid> DeliverAsync(Guid tenantId, object message) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), message, Cancellation);
            return tenantId;
        });

    private Task<Guid> DefaultPlanAsync() => database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans");

    private Task<Guid> PlanOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenantId}'");

    private Task<bool> InTrialAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<bool>($"SELECT plan_before_trial_id IS NOT NULL FROM control_plane.tenants WHERE id = '{tenantId}'");

    // The trial's plan, the plan of now, the plan it returns to, and its start and end in ticks.
    private Task<string?> TrialOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', trial_plan_id, plan_id, plan_before_trial_id,
                (extract(epoch FROM trial_started_at) * 10000000)::bigint + 621355968000000000,
                (extract(epoch FROM trial_ends_at) * 10000000)::bigint + 621355968000000000)
            FROM control_plane.tenants WHERE id = '{tenantId}'
            """);
}
