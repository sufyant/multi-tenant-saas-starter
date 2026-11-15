using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Plans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// POST /v1/platform/plans: a system admin creates a plan with a name, unique without regard to case, and a value of some keys of the
// code's list, each of its kind. A plan marked default takes the mark from the previous default plan.
public sealed class CreatePlanTests(Database database) : IAsyncLifetime
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
    public async Task CreatePlan_ARetentionUnderTheFloor_IsInvalidWithTheKey()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var created = await admin.CreatePlanAsync($"Plan {Guid.NewGuid():N}", new Dictionary<string, object> { ["audit.retention.days"] = PlanRequests.Max(29) });

        (created.StatusCode, await PlanRequests.ProblemOfAsync(created)).ShouldBe((HttpStatusCode.BadRequest, ("plan.entitlement_invalid", "audit.retention.days")));
    }

    // E1: a new plan's dependent limit is a change, so it needs its feature on in the same request.
    [Fact]
    public async Task CreatePlan_ADependentLimitWithItsFeatureOff_IsInvalidWithTheLimit()
    {
        var admin = await AdminAsync();

        var created = await admin.CreatePlanAsync($"Plan {Guid.NewGuid():N}", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.Off,
            ["roles.custom.max"] = PlanRequests.Max(5),
        });

        (created.StatusCode, await PlanRequests.ProblemOfAsync(created)).ShouldBe((HttpStatusCode.BadRequest, ("plan.feature_required", "roles.custom.max")));
    }

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

        var created = await admin.CreatePlanAsync("bUSINESS", new Dictionary<string, object>());

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

        var created = await api.WaitingForMessagesAsync(() => admin.CreatePlanAsync("Enterprise", new Dictionary<string, object>(), isDefault: true));

        var planId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        (await database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans", own)).ShouldBe(planId);
        var free = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/plans/{SeedPlans.Free}", Cancellation);
        free.GetProperty("isDefault").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task CreatePlan_PricesAndAMeter_AnswersThemWithTheCurrencyAndRecordsThem()
    {
        var admin = await AdminAsync();

        var created = await _api.WaitingForMessagesAsync(() => admin.CreatePlanAsync($"Team {Guid.NewGuid():N}", new Dictionary<string, object>(), TeamPrices, [AiRequests]));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var plan = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        plan.GetProperty("currency").GetString().ShouldBe("EUR");
        plan.GetProperty("prices").GetRawText().ShouldBe("""{"baseMonthly":0,"perPersonMonthly":1000,"baseYearly":0,"perPersonYearly":10000}""");
        plan.GetProperty("meters").GetRawText().ShouldBe("""[{"key":"ai.requests","quota":10000,"overage":"charge","unitPrice":100,"unitSize":1000}]""");
        var details = await _audit.PlatformDetailsAsync(plan.GetProperty("id").GetGuid());
        details!.ShouldContain("\"prices\": {\"baseYearly\": 0, \"baseMonthly\": 0, \"perPersonYearly\": 10000, \"perPersonMonthly\": 1000}");
        details!.ShouldContain("\"meters\": [{\"key\": \"ai.requests\", \"quota\": 10000, \"overage\": \"Charge\", \"unitSize\": 1000, \"unitPrice\": 100}]");
    }

    [Fact]
    public async Task CreatePlan_WithoutPricesAndMeters_IsFreeWithNoMeter()
    {
        var admin = await AdminAsync();

        var created = await _api.WaitingForMessagesAsync(() => admin.CreatePlanAsync($"Plan {Guid.NewGuid():N}", new Dictionary<string, object>()));

        var plan = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (plan.GetProperty("prices").GetRawText(), plan.GetProperty("meters").GetArrayLength())
            .ShouldBe(("""{"baseMonthly":0,"perPersonMonthly":0,"baseYearly":0,"perPersonYearly":0}""", 0));
    }

    [Fact]
    public async Task CreatePlan_ANegativePrice_IsInvalid()
    {
        var admin = await AdminAsync();

        var created = await admin.CreatePlanAsync(
            $"Plan {Guid.NewGuid():N}", new Dictionary<string, object>(), new { baseMonthly = -1, perPersonMonthly = 0, baseYearly = 0, perPersonYearly = 0 }, []);

        (created.StatusCode, await CodeOfAsync(created)).ShouldBe((HttpStatusCode.BadRequest, "plan.price_invalid"));
    }

    [Fact]
    public async Task CreatePlan_AMeterTheCodeDoesNotDefine_IsInvalid()
    {
        var admin = await AdminAsync();

        var created = await admin.CreatePlanAsync(
            $"Plan {Guid.NewGuid():N}", new Dictionary<string, object>(), TeamPrices, [new { key = "storage.bytes", quota = 1, overage = "stop", unitPrice = 0, unitSize = 1 }]);

        (created.StatusCode, await CodeOfAsync(created)).ShouldBe((HttpStatusCode.BadRequest, "plan.meter_invalid"));
    }

    [Fact]
    public async Task CreatePlan_AMeterSentTwice_IsInvalid()
    {
        var admin = await AdminAsync();

        var created = await admin.CreatePlanAsync($"Plan {Guid.NewGuid():N}", new Dictionary<string, object>(), TeamPrices, [AiRequests, AiRequests]);

        (created.StatusCode, await CodeOfAsync(created)).ShouldBe((HttpStatusCode.BadRequest, "plan.meter_invalid"));
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString();

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
