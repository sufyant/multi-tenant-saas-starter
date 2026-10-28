using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// The tenants and the answers of the plan checks, which the tests of several use cases share.
internal static class PlanAnswers
{
    // A tenant on the Free plan, roles.custom off, and its Owner.
    public static async Task<(Guid TenantId, HttpClient Owner)> FreeTenantAsync(TestData testData, ApiFactory api)
    {
        var tenant = await testData.AddTenantAsync(planId: SeedPlans.Free);
        return (tenant.Id, api.CreateClient(await testData.AddMemberAsync(tenant.Id, role: "Owner")));
    }

    // A tenant on a plan of its own with custom roles and this members.max, and its Owner.
    public static async Task<(Guid TenantId, HttpClient Owner)> TenantWithMembersMaxAsync(TestData testData, ApiFactory api, int membersMax)
    {
        var planId = await testData.AddPlanAsync(new() { ["roles.custom"] = true, ["roles.custom.max"] = 20, ["members.max"] = membersMax });
        var tenant = await testData.AddTenantAsync(planId: planId);
        return (tenant.Id, api.CreateClient(await testData.AddMemberAsync(tenant.Id, role: "Owner")));
    }

    // 403 plan.feature_unavailable, with the key roles.custom.
    public static async Task ShouldBeUnavailableAsync(HttpResponseMessage answer)
    {
        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await answer.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        (problem.GetProperty("code").GetString(), problem.GetProperty("entitlement").GetString()).ShouldBe(("plan.feature_unavailable", "roles.custom"));
    }
}
