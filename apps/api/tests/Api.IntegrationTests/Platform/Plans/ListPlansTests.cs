using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// GET /v1/platform/plans: every plan in the list contract, searched by name, with its default mark and how many tenants are on it.
public sealed class ListPlansTests(Database database) : IAsyncLifetime
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
    public async Task ListPlans_ASearch_AnswersTheMatchingPlansWithTheirTenantCounts()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var marker = $"{Guid.NewGuid():N}"[..8];
        var planId = await _testData.AddPlanAsync(new(), name: $"Team {marker}");
        await _testData.AddTenantAsync(planId: planId);
        await _testData.AddTenantAsync(planId: planId);

        var page = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/plans?q={marker}", Cancellation);

        page.GetProperty("total").GetInt32().ShouldBe(1);
        var plan = page.GetProperty("items").EnumerateArray().Single();
        (plan.GetProperty("id").GetGuid(), plan.GetProperty("isDefault").GetBoolean(), plan.GetProperty("tenantCount").GetInt32()).ShouldBe((planId, false, 2));
    }

    [Fact]
    public async Task ListPlans_TheSeedPlans_MarkFreeDefault()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var page = await admin.GetFromJsonAsync<JsonElement>("/v1/platform/plans?q=Free", Cancellation);

        page.GetProperty("items").EnumerateArray().Single(plan => plan.GetProperty("id").GetGuid() == SeedPlans.Free).GetProperty("isDefault").GetBoolean().ShouldBeTrue();
    }
}
