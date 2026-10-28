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

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
