using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Plans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Billing.Application.PriceChanges.AnnouncePriceChange;
using Billing.Application.PriceChanges.MovePlanPrices;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// PATCH /v1/platform/plans/{planId}: a plan's name, default mark and exactly these values; a key left out loses its value and is closed.
// The edit applies at once to every tenant on the plan: nothing is deleted, and over a limit nothing new is added.
public sealed class UpdatePlanTests(Database database) : IAsyncLifetime
{
    private static readonly object TeamPrices = new { baseMonthly = 0, perPersonMonthly = 1_000, baseYearly = 0, perPersonYearly = 10_000 };

    private static readonly object AiRequests = new { key = "ai.requests", quota = 10_000, overage = "charge", unitPrice = 100, unitSize = 1_000 };

    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        // The template defines no meter (step 40), so the host defines one of its own.
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            configureServices: services => services.Replace(ServiceDescriptor.Singleton(MeterCode.Of(("ai.requests", "AI requests the members make.")))));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task UpdatePlan_ARetentionUnderTheFloor_IsInvalidWithTheKey()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var plan = await _testData.AddPlanAsync(new() { ["audit.retention.days"] = 365 });

        var updated = await admin.UpdatePlanAsync(plan, $"Plan {Guid.NewGuid():N}", new Dictionary<string, object> { ["audit.retention.days"] = PlanRequests.Max(29) });

        (updated.StatusCode, await PlanRequests.ProblemOfAsync(updated)).ShouldBe((HttpStatusCode.BadRequest, ("plan.entitlement_invalid", "audit.retention.days")));
    }

    [Fact]
    public async Task UpdatePlan_NewValues_KeepsExactlyThemAndTheTenantsOnThePlanSeeThemAtOnce()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = false, ["members.max"] = 3 });
        var tenant = await _testData.AddTenantAsync(planId: planId);
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var before = await owner.CreateRoleAsync(tenant.Id, "Billing", ["tenant.profile.read"]);

        var updated = await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, "Renamed", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.On,
            ["roles.custom.max"] = PlanRequests.Max(5),
        }));
        var after = await owner.CreateRoleAsync(tenant.Id, "Billing", ["tenant.profile.read"]);

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plan = await updated.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (plan.GetProperty("name").GetString(), plan.GetProperty("tenantCount").GetInt32()).ShouldBe(("Renamed", 1));
        plan.GetProperty("entitlements").EnumerateObject().Select(value => value.Name).ShouldBe(["roles.custom", "roles.custom.max"], ignoreOrder: true);
        (before.StatusCode, after.StatusCode).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.Created));
    }

    // The default plan keeps its mark until another plan takes it.
    [Fact]
    public async Task UpdatePlan_TheDefaultMarkRemovedFromTheDefaultPlan_IsAConflictAndChangesNothing()
    {
        var admin = await AdminAsync();

        var updated = await admin.UpdatePlanAsync(SeedPlans.Free, "Renamed", new Dictionary<string, object>(), isDefault: false);

        updated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await PlanRequests.ProblemOfAsync(updated)).Code.ShouldBe("plan.default_required");
        (await database.ScalarAsSuperuserAsync<string>($"SELECT name FROM control_plane.plans WHERE id = '{SeedPlans.Free}'")).ShouldBe("Free");
    }

    // A plan becomes the default by marking it, and the mark leaves the previous default plan in the same transaction. It runs on a
    // database of its own, because the default plan of the shared one is every other test's.
    [Fact]
    public async Task UpdatePlan_MarkedDefault_TakesTheMarkFromThePreviousDefault()
    {
        var own = await database.CreateMigratedDatabaseAsync();
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, own));
        var admin = api.CreateClient(await new TestData(database, own).AddSystemAdminAsync(), secondFactor: true);

        var updated = await api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(SeedPlans.Business, "Business", new Dictionary<string, object>(), isDefault: true));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans", own)).ShouldBe(SeedPlans.Business);
        (await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM control_plane.default_plans", own)).ShouldBe(1);
        (await database.ScalarAsSuperuserAsync<string>($"SELECT details->>'previousDefaultPlan' FROM audit.platform_entries WHERE target_id = '{SeedPlans.Business}'", own))
            .ShouldBe($$"""{"id": "{{SeedPlans.Free}}", "name": "Free"}""");
    }

    [Fact]
    public async Task UpdatePlan_NoPlanWithTheId_IsNotFound()
    {
        var admin = await AdminAsync();

        var updated = await admin.UpdatePlanAsync(Guid.NewGuid(), "Nothing", new Dictionary<string, object>());

        (updated.StatusCode, (await PlanRequests.ProblemOfAsync(updated)).Code).ShouldBe((HttpStatusCode.NotFound, "plan.not_found"));
    }

    // A plan belongs to no tenant: its edit goes to the platform's log only, with the old and the new values.
    [Fact]
    public async Task UpdatePlan_NewValues_RecordsTheOldAndTheNewInThePlatformLog()
    {
        var adminId = await _testData.AddSystemAdminAsync();
        var admin = _api.CreateClient(adminId, secondFactor: true);
        var planId = await _testData.AddPlanAsync(new() { ["members.max"] = 3 }, name: $"Old {Guid.NewGuid():N}");

        await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, $"New {planId:N}", new Dictionary<string, object> { ["members.max"] = PlanRequests.Max(30) }));

        (await _audit.PlatformLogAsync(planId)).ShouldBe($"plan.updated|User|{await _testData.UserIdOfAsync(adminId)}|Plan|{planId}|New {planId:N}");
        (await _audit.PlatformDetailsAsync(planId))!.ShouldContain("\"oldEntitlements\": {\"members.max\": {\"enabled\": null, \"maximum\": 3, \"unlimited\": false}}");
    }

    // E1: a limit that depends on a feature changes only while the plan's value of the feature is on, after the same request's changes.
    [Fact]
    public async Task UpdatePlan_ADependentLimitChangedWithItsFeatureOff_IsInvalidWithTheLimitAndKeepsIt()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = false, ["roles.custom.max"] = 3 });

        var updated = await admin.UpdatePlanAsync(planId, $"Plan {planId:N}", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.Off,
            ["roles.custom.max"] = PlanRequests.Max(5),
        });

        (updated.StatusCode, await PlanRequests.ProblemOfAsync(updated)).ShouldBe((HttpStatusCode.BadRequest, ("plan.feature_required", "roles.custom.max")));
        (await StoredMaximumAsync(planId, "roles.custom.max")).ShouldBe(3);
    }

    [Fact]
    public async Task UpdatePlan_ADependentLimitChangedWithItsFeatureOn_SavesIt()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = true, ["roles.custom.max"] = 3 });

        var updated = await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, $"Plan {planId:N}", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.On,
            ["roles.custom.max"] = PlanRequests.Max(5),
        }));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredMaximumAsync(planId, "roles.custom.max")).ShouldBe(5);
    }

    [Fact]
    public async Task UpdatePlan_TheFeatureTurnedOnAndItsLimitChangedInOneRequest_SavesBoth()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = false, ["roles.custom.max"] = 3 });

        var updated = await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, $"Plan {planId:N}", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.On,
            ["roles.custom.max"] = PlanRequests.Max(5),
        }));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredMaximumAsync(planId, "roles.custom.max")).ShouldBe(5);
    }

    // Turning the feature off deletes no value: the plan keeps its limit, which applies again when the feature turns on again.
    [Fact]
    public async Task UpdatePlan_TheFeatureTurnedOff_KeepsTheStoredLimitForWhenItTurnsOnAgain()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = true, ["roles.custom.max"] = 1 });
        var tenant = await _testData.AddTenantAsync(planId: planId);
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));

        var off = await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, $"Plan {planId:N}", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.Off,
            ["roles.custom.max"] = PlanRequests.Max(1),
        }));
        var on = await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, $"Plan {planId:N}", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.On,
            ["roles.custom.max"] = PlanRequests.Max(1),
        }));
        var first = await owner.CreateRoleAsync(tenant.Id, "Billing", ["tenant.profile.read"]);
        var second = await owner.CreateRoleAsync(tenant.Id, "Support", ["tenant.profile.read"]);

        (off.StatusCode, on.StatusCode, first.StatusCode, second.StatusCode)
            .ShouldBe((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Conflict));
    }

    // An explicit deletion only closes the limit, so it stays allowed while the feature is off (the maintainer's decision of 2026-11-13).
    [Fact]
    public async Task UpdatePlan_ADependentLimitLeftOutWithItsFeatureOff_RemovesIt()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = false, ["roles.custom.max"] = 3 });

        var updated = await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, $"Plan {planId:N}", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.Off,
        }));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.plan_entitlements WHERE plan_id = '{planId}' AND key = 'roles.custom.max'")).ShouldBe(0);
    }

    // An edit gives the plan exactly the prices and the meters it sends; a request without them keeps the plan's (a client of the
    // version before step 40).
    [Fact]
    public async Task UpdatePlan_NewPricesAndNoMeter_KeepsTheEditAndAnEditWithoutThemKeepsThePlans()
    {
        var admin = await AdminAsync();
        var name = $"Team {Guid.NewGuid():N}";
        var created = await _api.WaitingForMessagesAsync(() => admin.CreatePlanAsync(name, new Dictionary<string, object>(), TeamPrices, [AiRequests]));
        var planId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        var newPrices = new { baseMonthly = 500, perPersonMonthly = 1_200, baseYearly = 5_000, perPersonYearly = 12_000 };

        var edited = await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, name, new Dictionary<string, object>(), newPrices, []));
        var kept = await admin.UpdatePlanAsync(planId, name, new Dictionary<string, object>());

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plan = await kept.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (plan.GetProperty("prices").GetRawText(), plan.GetProperty("meters").GetArrayLength())
            .ShouldBe(("""{"baseMonthly":500,"perPersonMonthly":1200,"baseYearly":5000,"perPersonYearly":12000}""", 0));
    }

    // An edit of the prices only is a change: it writes plan.updated with the old and the new prices.
    [Fact]
    public async Task UpdatePlan_OnlyThePrices_RecordsTheOldAndTheNewPrices()
    {
        var admin = await AdminAsync();
        var name = $"Team {Guid.NewGuid():N}";
        var created = await _api.WaitingForMessagesAsync(() => admin.CreatePlanAsync(name, new Dictionary<string, object>(), TeamPrices, [AiRequests]));
        var planId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

        await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(
            planId, name, new Dictionary<string, object>(), new { baseMonthly = 0, perPersonMonthly = 1_200, baseYearly = 0, perPersonYearly = 12_000 }, [AiRequests]));

        var details = await _audit.PlatformDetailsAsync(planId);
        details!.ShouldContain("\"oldPrices\": {\"baseYearly\": 0, \"baseMonthly\": 0, \"perPersonYearly\": 10000, \"perPersonMonthly\": 1000}");
        details!.ShouldContain("\"newPrices\": {\"baseYearly\": 0, \"baseMonthly\": 0, \"perPersonYearly\": 12000, \"perPersonMonthly\": 1200}");
    }

    // An edit of the meters only is a change too: removing the meter writes plan.updated with the meters before and after.
    [Fact]
    public async Task UpdatePlan_OnlyTheMeters_RecordsTheOldAndTheNewMeters()
    {
        var admin = await AdminAsync();
        var name = $"Team {Guid.NewGuid():N}";
        var created = await _api.WaitingForMessagesAsync(() => admin.CreatePlanAsync(name, new Dictionary<string, object>(), TeamPrices, [AiRequests]));
        var planId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

        await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, name, new Dictionary<string, object>(), TeamPrices, []));

        var details = await _audit.PlatformDetailsAsync(planId);
        details!.ShouldContain("\"newMeters\": []");
        details!.ShouldContain("\"oldMeters\": [{\"key\": \"ai.requests\", \"quota\": 10000, \"overage\": \"Charge\", \"unitSize\": 1000, \"unitPrice\": 100}]");
    }

    [Fact]
    public async Task UpdatePlan_AMeterTheCodeDoesNotDefine_IsInvalidAndChangesNothing()
    {
        var admin = await AdminAsync();
        var name = $"Team {Guid.NewGuid():N}";
        var created = await _api.WaitingForMessagesAsync(() => admin.CreatePlanAsync(name, new Dictionary<string, object>(), TeamPrices, [AiRequests]));
        var planId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

        var edited = await admin.UpdatePlanAsync(
            planId, name, new Dictionary<string, object>(), TeamPrices, [new { key = "storage.bytes", quota = 1, overage = "stop", unitPrice = 0, unitSize = 1 }]);

        (edited.StatusCode, await CodeOfAsync(edited)).ShouldBe((HttpStatusCode.BadRequest, "plan.meter_invalid"));
        var plan = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/plans/{planId}", Cancellation);
        plan.GetProperty("meters")[0].GetProperty("key").GetString().ShouldBe("ai.requests");
    }

    // 6.14: "Everyone, at their next period after 30 days' notice." Each paying organisation is told today, and its subscription moves to the
    // new Prices in 30 days, with no proration, so it pays the new price from its first period after that day (B13).
    [Fact]
    public async Task UpdatePlan_ANewPriceForEveryoneAfterNotice_TellsEachPayerTodayAndMovesItIn30Days()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        var (tenantId, _) = await billing.PayingAsync(billing.Tenants.Team);

        var session = await billing.Api.TrackMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "everyone_after_notice" }));

        var movesAt = billing.Time.GetUtcNow().AddDays(30);
        session.Scheduled.Envelopes().ShouldContain(envelope => envelope.Message is MovePlanPricesCommand && envelope.ScheduledTime == movesAt);
        (await billing.AuditCountAsync(tenantId, "tenant.price_change_announced")).ShouldBe(1);
        (await billing.PlatformAuditCountAsync(billing.Tenants.Team, "plan.price_change_scheduled")).ShouldBe(1);

        await billing.DeliverScheduledAsync(session);

        billing.Api.Billing.PriceMoves.ShouldBe([($"sub_{tenantId:N}", 0L, 1_200L)]);
    }

    // 6.14: "Everyone, after a set period": each payer is told 30 days before the day, and moves on the day.
    [Fact]
    public async Task UpdatePlan_ANewPriceFromADate_TellsEachPayer30DaysBeforeAndMovesItOnTheDay()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        var (tenantId, _) = await billing.PayingAsync(billing.Tenants.Team);
        var from = billing.Time.GetUtcNow().AddDays(180);

        var session = await billing.Api.TrackMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "everyone_from", from }));

        session.Scheduled.Envelopes().ShouldContain(envelope => envelope.Message is MovePlanPricesCommand && envelope.ScheduledTime == from);
        session.Scheduled.Envelopes().ShouldContain(envelope => envelope.Message is AnnouncePriceChangeCommand && envelope.ScheduledTime == from.AddDays(-30));
        (await billing.AuditCountAsync(tenantId, "tenant.price_change_announced")).ShouldBe(0);
    }

    // 6.14: "New customers only": today's payers keep their price, and nothing is scheduled.
    [Fact]
    public async Task UpdatePlan_ANewPriceForNewCustomersOnly_MovesNobody()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        await billing.PayingAsync(billing.Tenants.Team);

        var session = await billing.Api.TrackMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "new_customers_only" }));

        session.Scheduled.Envelopes().ShouldNotContain(envelope => envelope.Message is MovePlanPricesCommand || envelope.Message is AnnouncePriceChangeCommand);
        billing.Api.Billing.PriceMoves.ShouldBeEmpty();
    }

    // Only the plan's latest change moves anybody: a later edit replaces an earlier one (B13).
    [Fact]
    public async Task UpdatePlan_ASecondPriceChangeBeforeTheFirstMoves_MovesNobodyOnTheFirstsDay()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        await billing.PayingAsync(billing.Tenants.Team);
        var first = await billing.Api.TrackMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "everyone_after_notice" }));
        await billing.Api.WaitingForMessagesAsync(() => billing.ChangeTeamPriceAsync(
            new { who = "new_customers_only" }, new { baseMonthly = 0, perPersonMonthly = 1_500, baseYearly = 0, perPersonYearly = 15_000 }));

        await billing.DeliverScheduledAsync(first);

        billing.Api.Billing.PriceMoves.ShouldBeEmpty();
    }

    // A move that arrives before its day moves nobody: "Move to the new price" may have given the change a later day (B13).
    [Fact]
    public async Task UpdatePlan_AMoveThatArrivesBeforeItsDay_MovesNobody()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        await billing.PayingAsync(billing.Tenants.Team);
        var session = await billing.Api.TrackMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "everyone_after_notice" }));

        await billing.DeliverNowAsync(session.Scheduled.MessagesOf<MovePlanPricesCommand>().Single());

        billing.Api.Billing.PriceMoves.ShouldBeEmpty();
    }

    // The notice is 30 days, so a set day comes at least 30 days after today.
    [Fact]
    public async Task UpdatePlan_APriceChangeFromADayInLessThan30Days_IsInvalid()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync([]);
        await _testData.SetPlanPricesAsync(planId, 0, 1_000, 0, 10_000);

        var updated = await admin.PatchAsJsonAsync(
            $"/v1/platform/plans/{planId}",
            new
            {
                name = $"Team {Guid.NewGuid():N}",
                isDefault = false,
                entitlements = new Dictionary<string, object>(),
                prices = new { baseMonthly = 0, perPersonMonthly = 1_200, baseYearly = 0, perPersonYearly = 12_000 },
                priceChange = new { who = "everyone_from", from = DateTimeOffset.UtcNow.AddDays(29) },
            },
            Cancellation);

        (updated.StatusCode, await CodeOfAsync(updated)).ShouldBe((HttpStatusCode.BadRequest, "plan.price_change_invalid"));
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString();

    private Task<int> StoredMaximumAsync(Guid planId, string key) =>
        database.ScalarAsSuperuserAsync<int>($"SELECT maximum FROM control_plane.plan_entitlements WHERE plan_id = '{planId}' AND key = '{key}'");

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

}
