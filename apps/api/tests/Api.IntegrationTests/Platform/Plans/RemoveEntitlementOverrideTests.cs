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
