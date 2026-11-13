using System.Net;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// DELETE /v1/platform/tenants/{tenantId}/entitlement-overrides/{key}: the plan's value applies again. An override removed before is not
// found, and a closed tenant's overrides are read-only.
public sealed class RemoveEntitlementOverrideTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // An unlimited override removed: the plan's 365 days apply again, which is shorter.
    [Fact]
    public async Task RemoveEntitlementOverride_ThePlansShorterPeriodAppliesAgain_StartsTheHoldAtTheRemovalsMoment()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync();
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenant.Id, "audit.retention.days", PlanRequests.Unlimited))).EnsureSuccessStatusCode();

        (await _api.WaitingForMessagesAsync(() => admin.RemoveOverrideAsync(tenant.Id, "audit.retention.days"))).EnsureSuccessStatusCode();

        (await new AuditEntries(database).RetentionHoldAsync(tenant.Id, "tenant.entitlement_override_removed")).ShouldBe("365|t|t");
    }

    [Fact]
    public async Task RemoveEntitlementOverride_AnOverride_GivesThePlansValueBack()
    {
        var admin = await AdminAsync();
        var (tenantId, owner) = await PlanAnswers.FreeTenantAsync(_testData, _api);
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom", PlanRequests.On))).EnsureSuccessStatusCode();

        var removed = await _api.WaitingForMessagesAsync(() => admin.RemoveOverrideAsync(tenantId, "roles.custom"));
        var created = await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.profile.read"]);

        (removed.StatusCode, created.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.Forbidden));
    }

    // E1: removing an override of a dependent limit stays allowed while the feature is off, and turning the feature off removes no
    // override of its limit.
    [Fact]
    public async Task RemoveEntitlementOverride_TheFeatureOverrideRemoved_KeepsTheLimitOverrideWhichCanThenBeRemoved()
    {
        var admin = await AdminAsync();
        var (tenantId, _) = await PlanAnswers.FreeTenantAsync(_testData, _api);
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom", PlanRequests.On))).EnsureSuccessStatusCode();
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(5)))).EnsureSuccessStatusCode();

        var feature = await _api.WaitingForMessagesAsync(() => admin.RemoveOverrideAsync(tenantId, "roles.custom"));
        var kept = await database.ScalarAsSuperuserAsync<int>($"SELECT maximum FROM control_plane.entitlement_overrides WHERE tenant_id = '{tenantId}' AND key = 'roles.custom.max'");
        var limit = await _api.WaitingForMessagesAsync(() => admin.RemoveOverrideAsync(tenantId, "roles.custom.max"));

        (feature.StatusCode, kept, limit.StatusCode).ShouldBe((HttpStatusCode.NoContent, 5, HttpStatusCode.NoContent));
    }

    [Fact]
    public async Task RemoveEntitlementOverride_None_IsNotFound()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();

        var removed = await admin.RemoveOverrideAsync(tenant.Id, "roles.custom");

        (removed.StatusCode, (await PlanRequests.ProblemOfAsync(removed)).Code).ShouldBe((HttpStatusCode.NotFound, "plan.override_not_found"));
    }

    [Fact]
    public async Task RemoveEntitlementOverride_Closed_IsAConflictAndKeepsIt()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenant.Id, "members.max", PlanRequests.Unlimited))).EnsureSuccessStatusCode();
        await _testData.SetTenantStatusAsync(tenant.Id, "Closed");

        var removed = await admin.RemoveOverrideAsync(tenant.Id, "members.max");

        (removed.StatusCode, (await PlanRequests.ProblemOfAsync(removed)).Code).ShouldBe((HttpStatusCode.Conflict, "tenant.not_active"));
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.entitlement_overrides WHERE tenant_id = '{tenant.Id}'")).ShouldBe(1);
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
