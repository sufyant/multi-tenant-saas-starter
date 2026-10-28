using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// GET /v1/platform/plans/{planId}: one plan with its values; a key it has no value for is not listed, and is closed.
public sealed class GetPlanTests(Database database) : IAsyncLifetime
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
    public async Task GetPlan_Pro_AnswersItsSeedValues()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var plan = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/plans/{SeedPlans.Pro}", Cancellation);

        (plan.GetProperty("name").GetString(), plan.GetProperty("isDefault").GetBoolean()).ShouldBe(("Pro", false));
        plan.GetProperty("entitlements").GetRawText().ShouldBe(
            """{"members.max":{"enabled":null,"maximum":null,"unlimited":true},"roles.custom":{"enabled":true,"maximum":null,"unlimited":false},"roles.custom.max":{"enabled":null,"maximum":20,"unlimited":false}}""");
    }

    [Fact]
    public async Task GetPlan_NoPlanWithTheId_IsNotFound()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var found = await admin.GetAsync($"/v1/platform/plans/{Guid.NewGuid()}", Cancellation);

        found.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
