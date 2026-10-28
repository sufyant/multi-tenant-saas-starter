using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// PUT /v1/platform/tenants/{tenantId}/plan: a system admin puts the tenant on a plan. Its values apply at once; a tenant over a limit then
// loses nothing. A closed or purged tenant's plan is read-only (409 tenant.not_active); a suspended tenant's can change.
public sealed class ChangeTenantPlanTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // A Pro tenant with a custom role moves to Free: its member keeps the role and its permissions, a new custom role is refused, deleting
    // stays allowed.
    [Fact]
    public async Task ChangeTenantPlan_ToFree_AppliesAtOnceAndTakesNothingAway()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var role = await _testData.AddCustomRoleAsync(tenant.Id, "Viewer", "tenant.profile.read", "tenant.members.read");
        var member = _api.CreateClient(await _testData.AddMemberWithRoleAsync(tenant.Id, role));

        var changed = await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Free));

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plan = await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        plan.GetProperty("plan").GetProperty("name").GetString().ShouldBe("Free");
        (await member.GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.CreateRoleAsync(tenant.Id, "Billing", ["tenant.profile.read"])).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.roles WHERE tenant_id = '{tenant.Id}' AND built_in IS NULL")).ShouldBe(1);
    }

    [Fact]
    public async Task ChangeTenantPlan_Suspended_ChangesIt()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync(status: "Suspended", suspensionReason: "Unpaid invoices");

        var changed = await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Free));

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangeTenantPlan_Closed_IsAConflictAndKeepsThePlan()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantRowAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow, planId: SeedPlans.Pro);

        var changed = await admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Free);

        (changed.StatusCode, (await PlanRequests.ProblemOfAsync(changed)).Code).ShouldBe((HttpStatusCode.Conflict, "tenant.not_active"));
        (await database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenant.Id}'")).ShouldBe(SeedPlans.Pro);
    }

    [Fact]
    public async Task ChangeTenantPlan_Purged_IsAConflict()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantRowAsync(status: "Purged", planId: SeedPlans.Pro);

        var changed = await admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Free);

        (changed.StatusCode, (await PlanRequests.ProblemOfAsync(changed)).Code).ShouldBe((HttpStatusCode.Conflict, "tenant.not_active"));
    }

    [Fact]
    public async Task ChangeTenantPlan_NoPlanWithTheId_IsNotFound()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();

        var changed = await admin.ChangeTenantPlanAsync(tenant.Id, Guid.NewGuid());

        (changed.StatusCode, (await PlanRequests.ProblemOfAsync(changed)).Code).ShouldBe((HttpStatusCode.NotFound, "plan.not_found"));
    }

    // K3: the system admin acted in the platform's log, the platform in the tenant's; both keep the old and the new plan.
    [Fact]
    public async Task ChangeTenantPlan_ToAnotherPlan_RecordsItInBothLogs()
    {
        var adminId = await _testData.AddSystemAdminAsync();
        var admin = _api.CreateClient(adminId, secondFactor: true);
        var tenant = await _testData.AddTenantAsync(name: "Acme Ltd");

        await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Free));

        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBe($"tenant.plan_changed|User|{await _testData.UserIdOfAsync(adminId)}|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe($"{tenant.Id}:tenant.plan_changed|Platform|-|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe($$"""{"newPlanId": "{{SeedPlans.Free}}", "oldPlanId": "{{SeedPlans.Pro}}", "newPlanName": "Free", "oldPlanName": "Pro"}""");
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
