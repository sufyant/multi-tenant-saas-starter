using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Billing.Contracts.Subscriptions;
using ControlPlane.Application.Tenants.EndTenantTrial;
using ControlPlane.Application.Tenants.WarnTenantTrialEnding;
using ControlPlane.Contracts.Plans;
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

    private readonly NotificationRows _notifications = new(database);

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

    // The trial's end returns the tenant to its plan before; the platform acted, in both logs.
    [Fact]
    public async Task EndTenantTrial_AtItsEnd_ReturnsTheTenantToThePlanBeforeAndRecordsIt()
    {
        var (tenantId, scheduled) = await CreateTrackedAsync(await NewUserAsync());
        _time.Advance(TrialLength);

        await DeliverAsync(tenantId, scheduled.Single(each => each.Message is EndTenantTrialCommand).Message);

        (await PlanOfAsync(tenantId), await InTrialAsync(tenantId)).ShouldBe((await DefaultPlanAsync(), false));
        (await _audit.TenantLogAsync(tenantId))!.ShouldEndWith($"{tenantId}:tenant.trial_ended|Platform|-|Tenant|{tenantId}|Kestrel Analytics");
        (await _audit.PlatformLogAsync(tenantId))!.ShouldEndWith($"tenant.trial_ended|Platform|-|Tenant|{tenantId}|Kestrel Analytics");
    }

    // 13.16, 3.19: 3 days before the end, the members who manage billing hear that the trial ends, and which plan comes then.
    [Fact]
    public async Task WarnTenantTrialEnding_ThreeDaysBefore_TellsTheMembersWhoManageBilling()
    {
        var person = await NewUserAsync();
        var (tenantId, scheduled) = await CreateTrackedAsync(person);
        var (warning, at) = scheduled.Single(each => each.Message is WarnTenantTrialEndingCommand);
        _time.Advance(at!.Value - _time.GetUtcNow());

        await DeliverAsync(tenantId, warning);

        var userId = await _testData.UserIdOfAsync(person);
        (await _notifications.ValueListOfAsync(userId, "tenant.trial_ending", tenantId)).ShouldBe(
            $"days=3, defaultPlanName={await DefaultPlanNameAsync()}, endsAt={Moment(at.Value + TimeSpan.FromDays(3))}, planName=Business, tenantName=Kestrel Analytics");
    }

    // O4: a copy of the reminder, as a retry delivers, tells each member once.
    [Fact]
    public async Task WarnTenantTrialEnding_Twice_TellsTheMembersOnce()
    {
        var (tenantId, scheduled) = await CreateTrackedAsync(await NewUserAsync());
        var (warning, at) = scheduled.Single(each => each.Message is WarnTenantTrialEndingCommand);
        _time.Advance(at!.Value - _time.GetUtcNow());

        await DeliverAsync(tenantId, warning);
        await DeliverAsync(tenantId, warning);

        (await _notifications.CountAboutAsync(tenantId, "tenant.trial_ending")).ShouldBe(1);
    }

    // K4: a reminder of an end the trial no longer has, as an extension leaves behind, tells nobody.
    [Fact]
    public async Task WarnTenantTrialEnding_OfAnotherEnd_TellsNobody()
    {
        var (tenantId, _) = await CreateTrackedAsync(await NewUserAsync());

        await DeliverAsync(tenantId, new WarnTenantTrialEndingCommand(tenantId, _time.GetUtcNow() + TimeSpan.FromDays(5)));

        (await _notifications.CountAboutAsync(tenantId, "tenant.trial_ending")).ShouldBe(0);
    }

    // 13.17, 3.20: at the end, the members who manage billing hear that the tenant moved back, and that nothing was deleted.
    [Fact]
    public async Task EndTenantTrial_AtItsEnd_TellsTheMembersWhoManageBilling()
    {
        var person = await NewUserAsync();
        var (tenantId, scheduled) = await CreateTrackedAsync(person);
        _time.Advance(TrialLength);

        await DeliverAsync(tenantId, scheduled.Single(each => each.Message is EndTenantTrialCommand).Message);

        var userId = await _testData.UserIdOfAsync(person);
        (await _notifications.ValueListOfAsync(userId, "tenant.trial_ended", tenantId)).ShouldBe(
            $"defaultPlanName={await DefaultPlanNameAsync()}, endedAt={Moment(_time.GetUtcNow())}, planName=Business, tenantName=Kestrel Analytics");
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

    // 4.29: a tenant that pays by card for Team, in a Business trial that a system admin gave. Billing sends Team again at each change of the
    // subscription; that keeps the trial and announces nothing.
    [Fact]
    public async Task ChangeBilledPlan_ThePlanTheTrialReturnsTo_KeepsTheTrial()
    {
        var tenant = await _testData.AddTenantAsync();
        await Trials.StartAsync(database, tenant.Id, SeedPlans.Team);

        var session = await _api.TrackMessagesAsync(
            () => DeliverAsync(tenant.Id, new PlanPaidEvent(Guid.NewGuid(), _time.GetUtcNow(), tenant.Id, SeedPlans.Team, Guid.NewGuid())));

        (await PlanOfAsync(tenant.Id), await InTrialAsync(tenant.Id)).ShouldBe((SeedPlans.Business, true));
        session.Sent.MessagesOf<TenantPlanChangedEvent>().ShouldBeEmpty();
    }

    // A subscription that ends during the trial ends the trial too, and moves the tenant to the default plan at once (B11).
    [Fact]
    public async Task MoveToDefaultPlan_InTrial_EndsTheTrialAndMovesTheTenant()
    {
        var tenant = await _testData.AddTenantAsync(name: "Trial Ltd");
        await Trials.StartAsync(database, tenant.Id, SeedPlans.Team);

        await DeliverAsync(tenant.Id, new PlanEndedEvent(Guid.NewGuid(), _time.GetUtcNow(), tenant.Id, Guid.NewGuid(), Unpaid: true));

        (await PlanOfAsync(tenant.Id), await InTrialAsync(tenant.Id)).ShouldBe((await DefaultPlanAsync(), false));
        (await _audit.TenantLogAsync(tenant.Id))!.ShouldEndWith($"{tenant.Id}:tenant.moved_to_default_plan|Platform|-|Tenant|{tenant.Id}|Trial Ltd");
    }

    // A moment as a Date value of a notification stores it.
    private static string Moment(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

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

    private Task<string?> DefaultPlanNameAsync() =>
        database.ScalarAsSuperuserAsync<string>("SELECT name FROM control_plane.plans WHERE id = (SELECT plan_id FROM control_plane.default_plans)");

    private Task<Guid> DefaultPlanAsync() => database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans");

    private Task<Guid> PlanOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenantId}'");

    private Task<bool> InTrialAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<bool>($"SELECT plan_before_trial_id IS NOT NULL FROM control_plane.tenants WHERE id = '{tenantId}'");
}
