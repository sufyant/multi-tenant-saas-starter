using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// The tenants and the answers of the plan checks, which the tests of several use cases share.
internal static class PlanAnswers
{
    public const string RetentionKey = "audit.retention.days";

    // A plan of the test's own with this Activity log period; 0 is unlimited.
    public static Task<Guid> RetentionPlanAsync(TestData testData, int days) =>
        testData.AddPlanAsync(new() { [RetentionKey] = days == 0 ? TestData.Unlimited : days });

    // A preview's retentionShortened as "from>to", with "unlimited" for no limit, a count first when it has one ("4:365>90"), or "none"
    // when it is null.
    public static string RetentionShortened(JsonElement preview) =>
        preview.GetProperty("retentionShortened") is { ValueKind: JsonValueKind.Object } shortened
            ? $"{CountOf(shortened)}{DaysOf(shortened.GetProperty("fromDays"))}>{shortened.GetProperty("toDays").GetInt32()}"
            : "none";

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

    private static string CountOf(JsonElement shortened) => shortened.TryGetProperty("count", out var count) ? $"{count.GetInt32()}:" : string.Empty;

    private static string DaysOf(JsonElement days) => days.ValueKind == JsonValueKind.Null ? "unlimited" : $"{days.GetInt32()}";
}
