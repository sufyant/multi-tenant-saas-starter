using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// POST /v1/platform/plans/{planId}/preview: before an edit, the tenants on the plan that the new values would put over a limit, or whose
// feature in use they would turn off, with each key, the count and the new value. A page in the list contract, searched by name or slug,
// the tenant that goes furthest over first, with the total. It reads the counts from the tenants' summaries, across tenants (R12).
public sealed class GetPlanUpdatePreviewTests(Database database) : IAsyncLifetime
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

    // Three tenants on the plan: one with three custom roles, one with five members, one within. The new values allow one custom role and
    // three members. Both go two over, so the name orders them.
    [Fact]
    public async Task GetPlanUpdatePreview_LowerLimits_ListsExactlyTheTenantsAndKeysOverTheLimitFurthestFirst()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = true, ["roles.custom.max"] = 10, ["members.max"] = 10 });
        var roles = await TenantAsync(planId, "Roles Ltd", customRoles: 3, members: 1);
        var members = await TenantAsync(planId, "Members Ltd", customRoles: 0, members: 5);
        await TenantAsync(planId, "Within Ltd", customRoles: 1, members: 2);

        var previewed = await admin.GetPlanUpdatePreviewAsync(planId, NewValues());

        previewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await previewed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        page.GetProperty("total").GetInt32().ShouldBe(2);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => (item.GetProperty("tenantId").GetGuid(), item.GetProperty("furthest").GetInt32())).ShouldBe([(members, 2), (roles, 2)]);
        items.Select(item => string.Join(",", item.GetProperty("excesses").EnumerateArray().Select(excess =>
            $"{excess.GetProperty("key").GetString()}:{excess.GetProperty("count").GetInt32()}>{excess.GetProperty("newValue").GetProperty("maximum").GetInt32()}")))
            .ShouldBe(["members.max:5>3", "roles.custom.max:3>1"]);
    }

    // A feature that turns off while the tenant uses it is listed with what uses it; a key an override still holds is not, and is named.
    [Fact]
    public async Task GetPlanUpdatePreview_AFeatureOffAndAnOverride_ListsTheFeatureInUseAndNamesTheOverriddenKey()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = true, ["roles.custom.max"] = 20, ["members.max"] = TestData.Unlimited });
        var tenant = await TenantAsync(planId, "Acme Ltd", customRoles: 14, members: 6);
        (await admin.SetOverrideAsync(tenant, "members.max", PlanRequests.Unlimited)).EnsureSuccessStatusCode();

        var previewed = await admin.GetPlanUpdatePreviewAsync(planId, new Dictionary<string, object> { ["roles.custom.max"] = PlanRequests.Max(20), ["members.max"] = PlanRequests.Max(5) });

        var item = (await previewed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("items").EnumerateArray().Single();
        var excess = item.GetProperty("excesses").EnumerateArray().Single();
        (excess.GetProperty("key").GetString(), excess.GetProperty("count").GetInt32(), excess.GetProperty("newValue").GetProperty("enabled").GetBoolean()).ShouldBe(("roles.custom", 14, false));
        item.GetProperty("overriddenKeys").EnumerateArray().Select(key => key.GetString()).ShouldBe(["members.max"]);
    }

    [Fact]
    public async Task GetPlanUpdatePreview_ASearchAndASecondPage_PagesTheMatchingTenants()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = true, ["roles.custom.max"] = 10, ["members.max"] = 10 });
        var marker = $"{Guid.NewGuid():N}"[..8];
        await TenantAsync(planId, $"Alpha {marker}", customRoles: 4, members: 1);
        var second = await TenantAsync(planId, $"Beta {marker}", customRoles: 3, members: 1);
        await TenantAsync(planId, "Gamma Ltd", customRoles: 9, members: 1);

        var previewed = await admin.GetPlanUpdatePreviewAsync(planId, NewValues(), $"?q={marker}&pageSize=1&page=2");

        var page = await previewed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        page.GetProperty("total").GetInt32().ShouldBe(2);
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("tenantId").GetGuid()).ShouldBe([second]);
    }

    private static Dictionary<string, object> NewValues() =>
        new() { ["roles.custom"] = PlanRequests.On, ["roles.custom.max"] = PlanRequests.Max(1), ["members.max"] = PlanRequests.Max(3) };

    private async Task<Guid> TenantAsync(Guid planId, string name, int customRoles, int members)
    {
        var tenant = await _testData.AddTenantAsync(name: name, planId: planId);
        foreach (var index in Enumerable.Range(0, customRoles))
        {
            await _testData.AddCustomRoleAsync(tenant.Id, $"Role {index}", "tenant.profile.read");
        }

        foreach (var _ in Enumerable.Range(0, members))
        {
            await _testData.AddMemberAsync(tenant.Id);
        }

        await _testData.SummarizeAsync(tenant.Id);
        return tenant.Id;
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
