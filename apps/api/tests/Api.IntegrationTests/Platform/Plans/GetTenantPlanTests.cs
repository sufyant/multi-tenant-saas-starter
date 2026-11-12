using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// GET /v1/platform/tenants/{tenantId}/plan: the tenant's plan, and for every key of the list the plan's value, the override, the
// effective value and what the tenant has that the key counts, from its summary.
public sealed class GetTenantPlanTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // E3: an effective read answers a missing value as unlimited; the plan's own value stays not set.
    [Fact]
    public async Task GetTenantPlan_APlanWithoutTheRetention_IsUnlimited()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync(planId: await _testData.AddPlanAsync(new()));

        var entry = PlanRequests.EntitlementOf(await admin.TenantPlanAsync(tenant.Id), "audit.retention.days");

        entry.GetProperty("effective").GetProperty("unlimited").GetBoolean().ShouldBeTrue();
        entry.GetProperty("planValue").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task GetTenantPlan_AProTenant_AnswersEveryKeyWithItsValuesAndCount()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.profile.read");
        await _testData.AddMemberAsync(tenant.Id);
        await _testData.SummarizeAsync(tenant.Id);

        var plan = await admin.TenantPlanAsync(tenant.Id);

        plan.GetProperty("plan").GetProperty("id").GetGuid().ShouldBe(SeedPlans.Pro);
        plan.GetProperty("entitlements").EnumerateArray().Select(entry => entry.GetProperty("key").GetString()).ShouldBe(["audit.retention.days", "members.max", "roles.custom", "roles.custom.max"]);
        var limit = PlanRequests.EntitlementOf(plan, "roles.custom.max");
        (limit.GetProperty("isFeature").GetBoolean(), limit.GetProperty("planValue").GetProperty("maximum").GetInt32(), limit.GetProperty("count").GetInt32())
            .ShouldBe((false, 20, 1));
        limit.GetProperty("override").ValueKind.ShouldBe(JsonValueKind.Null);
        PlanRequests.EntitlementOf(plan, "members.max").GetProperty("effective").GetProperty("unlimited").GetBoolean().ShouldBeTrue();
    }
}
