using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Tenant.FeatureFlags;

// GET /v1/tenants/{tenantId}/feature-flags: every flag the code defines, on or off for the member's organisation. The template's code
// defines one kill switch, which the shared database does not store (its hosts run no sync), so it answers its default value, on; the
// evaluation itself is ControlPlane's FeatureFlagEvaluationTests.
public sealed class GetMyTenantFeatureFlagsTests(Database database)
{
    [Fact]
    public async Task GetMyTenantFeatureFlags_AKillSwitchNotStoredYet_ReadsItsDefaultValue()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var data = new TestData(database);
        var (tenantId, _) = await data.AddTenantAsync();
        var member = api.CreateClient(await data.AddMemberAsync(tenantId));

        var flags = await member.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenantId}/feature-flags", TestContext.Current.CancellationToken);

        flags.GetProperty("flags").ToString().ShouldBe("""{"emails.send":true}""");
    }
}
