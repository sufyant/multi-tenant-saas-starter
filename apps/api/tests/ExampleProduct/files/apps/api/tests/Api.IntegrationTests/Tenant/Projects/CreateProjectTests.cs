using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Projects;

// POST /v1/tenants/{tenantId}/projects, the example product's module (X4, X8): a member whose role holds tenant.projects.create creates a
// project while the plan has projects.access, up to projects.max (X1, X3). The tenant's Activity log records project.created by the
// member (X8), and the tenant's Owners hear of it in the bell (X7).
public sealed class CreateProjectTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private readonly NotificationRows _notifications = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CreateProject_ByAMember_CreatesItRecordsItAndTellsTheOwners()
    {
        var tenant = await TenantOnAPlanAsync(limit: 2);
        var owner = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant, role: "Owner"));
        var member = await _testData.AddMemberAsync(tenant);
        var memberId = await _testData.UserIdOfAsync(member);

        var created = await _api.WaitingForMessagesAsync(() => CreateAsync(_api.CreateClient(member), tenant, "  Apollo  "));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("name").GetString(), body.GetProperty("createdBy").GetGuid()).ShouldBe(("Apollo", memberId));
        (await _audit.TenantLogAsync(tenant)).ShouldBe($"{tenant}:project.created|User|{memberId}|Tenant|{tenant}|-");
        (await _notifications.OfAsync(owner, memberId)).ShouldBe([NotificationRows.Row(owner, "project.created", tenant, "Tenant")]);
        (await _notifications.ValueListOfAsync(owner, "project.created")).ShouldBe($"actorName={memberId}, projectName=Apollo, tenantName=Acme Ltd");
    }

    // X3: the module counts its rows under its own lock, against the limit read from the database.
    [Fact]
    public async Task CreateProject_AtTheLimit_IsAConflictAndCreatesNothing()
    {
        var tenant = await TenantOnAPlanAsync(limit: 1);
        var member = _api.CreateClient(await _testData.AddMemberAsync(tenant));
        (await CreateAsync(member, tenant, "Apollo")).StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await CreateAsync(member, tenant, "Gemini");

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (problem.GetProperty("code").GetString(), problem.GetProperty("entitlement").GetString()).ShouldBe(("plan.limit_reached", "projects.max"));
        (await ProjectsOfAsync(tenant)).ShouldBe(1);
    }

    // X1, E5: a product feature gates a product endpoint as a template feature does.
    [Fact]
    public async Task CreateProject_WithoutTheFeature_IsForbiddenAndCreatesNothing()
    {
        var plan = await _testData.AddPlanAsync(new() { ["projects.access"] = false, ["projects.max"] = TestData.Unlimited, ["members.max"] = TestData.Unlimited });
        var (tenant, _) = await _testData.AddTenantAsync(planId: plan);
        var member = _api.CreateClient(await _testData.AddMemberAsync(tenant));

        var created = await CreateAsync(member, tenant, "Apollo");

        created.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (problem.GetProperty("code").GetString(), problem.GetProperty("entitlement").GetString()).ShouldBe(("plan.feature_unavailable", "projects.access"));
        (await ProjectsOfAsync(tenant)).ShouldBe(0);
    }

    private static async Task<HttpResponseMessage> CreateAsync(HttpClient client, Guid tenantId, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/projects") { Content = JsonContent.Create(new { name }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request, Cancellation);
    }

    private async Task<Guid> TenantOnAPlanAsync(int limit)
    {
        var plan = await _testData.AddPlanAsync(new() { ["projects.access"] = true, ["projects.max"] = limit, ["members.max"] = TestData.Unlimited });
        return (await _testData.AddTenantAsync(planId: plan)).Id;
    }

    private Task<long> ProjectsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM projects.projects WHERE tenant_id = '{tenantId}'");
}
