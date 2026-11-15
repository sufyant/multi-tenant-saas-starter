using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Plans;

// GET /v1/tenants/{tenantId}/plans: every plan, for the Change plan page of a member who manages billing (B9, 10.02), the cheapest
// monthly price first, with its values, its prices in the deployment's currency, its meters, and the default plan marked.
public sealed class GetPlanChoicesTests(Database database) : IAsyncLifetime
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
    public async Task GetPlanChoices_ForABillingManager_AnswersThePlanWithItsValuesAndPrices()
    {
        var name = $"Team {Guid.NewGuid():N}";
        var planId = await _testData.AddPlanAsync(new() { ["members.max"] = 15 }, name: name);
        await _testData.SetPlanPricesAsync(planId, 0, 1000, 0, 10000);
        var tenant = await _testData.AddTenantAsync();
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");

        var answer = await _api.CreateClient(manager).GetPlanChoicesAsync(tenant.Id);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plan = (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("plans").EnumerateArray()
            .Single(choice => choice.GetProperty("id").GetGuid() == planId);
        (plan.GetProperty("name").GetString(), plan.GetProperty("isDefault").GetBoolean(), plan.GetProperty("currency").GetString()).ShouldBe((name, false, "EUR"));
        (plan.GetProperty("entitlements").GetProperty("members.max").GetInt32(), plan.GetProperty("prices").GetProperty("perPersonYearly").GetInt64()).ShouldBe((15, 10000L));
    }

    [Fact]
    public async Task GetPlanChoices_ForAnOwner_MarksTheDefaultPlan()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var defaultPlan = await database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans");

        var plans = (await (await _api.CreateClient(owner).GetPlanChoicesAsync(tenant.Id)).Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("plans");

        plans.EnumerateArray().Where(plan => plan.GetProperty("isDefault").GetBoolean()).Select(plan => plan.GetProperty("id").GetGuid()).ShouldBe([defaultPlan]);
    }

    [Fact]
    public async Task GetPlanChoices_ByAMember_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);

        var answer = await _api.CreateClient(member).GetPlanChoicesAsync(tenant.Id);

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
