using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// PATCH /v1/platform/plans/{planId}: a plan's name, default mark and exactly these values; a key left out loses its value and is closed.
// The edit applies at once to every tenant on the plan: nothing is deleted, and over a limit nothing new is added.
public sealed class UpdatePlanTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
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

        var updated = await api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(SeedPlans.Pro, "Pro", new Dictionary<string, object>(), isDefault: true));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans", own)).ShouldBe(SeedPlans.Pro);
        (await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM control_plane.default_plans", own)).ShouldBe(1);
        (await database.ScalarAsSuperuserAsync<string>($"SELECT details->>'previousDefaultPlan' FROM audit.platform_entries WHERE target_id = '{SeedPlans.Pro}'", own))
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

    private Task<int> StoredMaximumAsync(Guid planId, string key) =>
        database.ScalarAsSuperuserAsync<int>($"SELECT maximum FROM control_plane.plan_entitlements WHERE plan_id = '{planId}' AND key = '{key}'");

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
