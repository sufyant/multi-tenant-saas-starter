using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// POST /v1/platform/plans: a system admin creates a plan with a name, unique without regard to case, and a value of some keys of the
// code's list, each of its kind. A plan marked default takes the mark from the previous default plan.
public sealed class CreatePlanTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CreatePlan_ANameAndValues_CreatesThePlanWithItsAddress()
    {
        var admin = await AdminAsync();
        var name = $"Team {Guid.NewGuid():N}";

        var created = await _api.WaitingForMessagesAsync(() => admin.CreatePlanAsync($"  {name}  ", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.On,
            ["members.max"] = PlanRequests.Unlimited,
        }));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var plan = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        created.Headers.Location!.ToString().ShouldEndWith($"/v1/platform/plans/{plan.GetProperty("id").GetGuid()}");
        (plan.GetProperty("name").GetString(), plan.GetProperty("isDefault").GetBoolean(), plan.GetProperty("tenantCount").GetInt32()).ShouldBe((name, false, 0));
        plan.GetProperty("entitlements").GetRawText().ShouldBe("""{"members.max":{"enabled":null,"maximum":null,"unlimited":true},"roles.custom":{"enabled":true,"maximum":null,"unlimited":false}}""");
    }

    [Fact]
    public async Task CreatePlan_ANameAnotherPlanHasInAnyCase_IsAConflict()
    {
        var admin = await AdminAsync();

        var created = await admin.CreatePlanAsync("pRO", new Dictionary<string, object>());

        created.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await PlanRequests.ProblemOfAsync(created)).Code.ShouldBe("plan.name_taken");
    }

    [Theory]
    [InlineData("projects.max")]
    [InlineData("roles.custom")]
    public async Task CreatePlan_AKeyNotOnTheListOrAValueOfTheWrongKind_IsInvalidWithTheKey(string key)
    {
        var admin = await AdminAsync();

        var created = await admin.CreatePlanAsync($"Plan {Guid.NewGuid():N}", new Dictionary<string, object> { [key] = PlanRequests.Max(3) });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PlanRequests.ProblemOfAsync(created)).ShouldBe(("plan.entitlement_invalid", key));
    }

    [Fact]
    public async Task CreatePlan_TwoFieldsOfAValue_IsInvalidWithTheKey()
    {
        var admin = await AdminAsync();

        var created = await admin.CreatePlanAsync($"Plan {Guid.NewGuid():N}", new Dictionary<string, object> { ["members.max"] = new { maximum = 3, unlimited = true } });

        (await PlanRequests.ProblemOfAsync(created)).ShouldBe(("plan.entitlement_invalid", "members.max"));
    }

    // Exactly one plan is default: a new plan marked default takes the mark, and the previous default plan loses it. It runs on a database
    // of its own, because the default plan of the shared one is every other test's.
    [Fact]
    public async Task CreatePlan_MarkedDefault_TakesTheMarkFromThePreviousDefault()
    {
        var own = await database.CreateMigratedDatabaseAsync();
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, own));
        var admin = api.CreateClient(await new TestData(database, own).AddSystemAdminAsync(), secondFactor: true);

        var created = await api.WaitingForMessagesAsync(() => admin.CreatePlanAsync("Business", new Dictionary<string, object>(), isDefault: true));

        var planId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        (await database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans", own)).ShouldBe(planId);
        var free = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/plans/{SeedPlans.Free}", Cancellation);
        free.GetProperty("isDefault").GetBoolean().ShouldBeFalse();
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
