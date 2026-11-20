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

    // A system admin who gives the trial's own plan ends the trial and keeps the plan: no change of plan is recorded (I1).
    [Fact]
    public async Task ChangeTenantPlan_TheTrialsPlan_EndsTheTrialAndRecordsNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        await Trials.StartAsync(database, tenant.Id, SeedPlans.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var changed = await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Business));

        changed.EnsureSuccessStatusCode();
        (await database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenant.Id}'"), await Trials.InTrialAsync(database, tenant.Id)).ShouldBe((SeedPlans.Business, false));
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task ChangeTenantPlan_ToAShorterPeriod_StartsTheHoldAtTheChangesMoment()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync();
        var plan = await _testData.AddPlanAsync(new() { ["audit.retention.days"] = 90 });

        (await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenant.Id, plan))).EnsureSuccessStatusCode();

        (await new AuditEntries(database).RetentionHoldAsync(tenant.Id, "tenant.plan_changed")).ShouldBe("90|t|t");
    }

    [Fact]
    public async Task ChangeTenantPlan_ToALongerPeriod_WritesNoRow()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync(planId: await _testData.AddPlanAsync(new() { ["audit.retention.days"] = 90 }));

        (await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Business))).EnsureSuccessStatusCode();

        (await new AuditEntries(database).RetentionHoldAsync(tenant.Id, "tenant.plan_changed")).ShouldBeNull();
    }

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
        var tenant = await _testData.AddTenantRowAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow, planId: SeedPlans.Business);

        var changed = await admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Free);

        (changed.StatusCode, (await PlanRequests.ProblemOfAsync(changed)).Code).ShouldBe((HttpStatusCode.Conflict, "tenant.not_active"));
        (await database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenant.Id}'")).ShouldBe(SeedPlans.Business);
    }

    [Fact]
    public async Task ChangeTenantPlan_Purged_IsAConflict()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantRowAsync(status: "Purged", planId: SeedPlans.Business);

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
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe($$$"""{"newPlan": {"id": "{{{SeedPlans.Free}}}", "name": "Free"}, "oldPlan": {"id": "{{{SeedPlans.Business}}}", "name": "Business"}}""");
    }

    [Fact]
    public async Task ChangeTenantPlan_ATenantThatPaysByCard_IsRefusedAndKeepsItsPlan()
    {
        var tenants = await BillingOnAsync();
        var (tenantId, _) = await tenants.PayingAsync(tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var response = await admin.ChangeTenantPlanAsync(tenantId, await tenants.DefaultPlanAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(response)).ShouldBe("tenant.pays_by_card");
        (await tenants.PlanOfAsync(tenantId)).ShouldBe(tenants.Team);
    }

    [Fact]
    public async Task ChangeTenantPlan_AContractTenant_ChangesThePlan()
    {
        var tenants = await BillingOnAsync();
        var (tenantId, _) = await tenants.PayingAsync(tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));
        var plan = await tenants.DefaultPlanAsync();

        var response = await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenantId, plan));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tenants.PlanOfAsync(tenantId)).ShouldBe(plan);
    }

    // B12: a tenant that never paid by card has no subscription to set its plan, so it takes the system admin's plan.
    [Fact]
    public async Task ChangeTenantPlan_ATenantThatNeverPaidWhileBillingIsOn_ChangesThePlan()
    {
        var tenants = await BillingOnAsync();
        var tenant = await _testData.AddTenantAsync();
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var response = await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenant.Id, tenants.Team));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tenants.PlanOfAsync(tenant.Id)).ShouldBe(tenants.Team);
    }

    // B12: back to card, the subscription sets the plan again.
    [Fact]
    public async Task ChangeTenantPlan_AContractTenantBackToCard_IsRefused()
    {
        var tenants = await BillingOnAsync();
        var (tenantId, _) = await tenants.PayingAsync(tenants.Team);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "contract"));
        await _api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(tenantId, "card"));

        var response = await admin.ChangeTenantPlanAsync(tenantId, await tenants.DefaultPlanAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BillingRequests.CodeOfAsync(response)).ShouldBe("tenant.pays_by_card");
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    // Step 40b: billing is on for this test alone, with its plans and Stripe's fake.
    private async Task<PayingTenants> BillingOnAsync()
    {
        await _api.DisposeAsync();
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: BillingRequests.On);
        var tenants = new PayingTenants(database, _api);
        await tenants.AddPlansAsync();
        return tenants;
    }
}
