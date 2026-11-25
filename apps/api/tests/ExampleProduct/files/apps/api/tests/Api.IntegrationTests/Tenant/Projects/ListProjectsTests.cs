using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Projects;

// GET /v1/tenants/{tenantId}/projects, the example product's list (X4): a member whose role holds tenant.projects.read reads the tenant's
// projects, sorted and searched by name (K5), and row level security keeps them to the tenant (R1).
public sealed class ListProjectsTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ListProjects_TwoTenantsWithProjects_ListsOnlyTheTenantsOwnSortedAndSearched()
    {
        var plan = await _testData.AddPlanAsync(new() { ["projects.access"] = true, ["projects.max"] = TestData.Unlimited, ["members.max"] = TestData.Unlimited });
        var (tenant, _) = await _testData.AddTenantAsync(planId: plan);
        var (other, _) = await _testData.AddTenantAsync(planId: plan, name: "Globex");
        var member = _api.CreateClient(await _testData.AddMemberAsync(tenant));
        var stranger = _api.CreateClient(await _testData.AddMemberAsync(other));
        await CreateAsync(member, tenant, "Gemini");
        await CreateAsync(member, tenant, "Apollo");
        await CreateAsync(member, tenant, "Artemis");
        await CreateAsync(stranger, other, "Apollo II");

        // "Gemini" has no "a", so the search leaves it out.
        var listed = await member.GetAsync($"/v1/tenants/{tenant}/projects?sort=-name&q=a", Cancellation);

        listed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await listed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        body.GetProperty("items").EnumerateArray().Select(project => project.GetProperty("name").GetString()).ShouldBe(["Artemis", "Apollo"]);
        body.GetProperty("total").GetInt32().ShouldBe(2);
    }

    private static async Task CreateAsync(HttpClient client, Guid tenantId, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/projects") { Content = JsonContent.Create(new { name }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await client.SendAsync(request, Cancellation)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
