using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Plans;

// A feature of the plan that is off takes nothing away that exists: with roles.custom off (the Free plan), a member who holds a custom
// role keeps its permissions on every endpoint. The tests of each refused use case sit in that use case's class.
public sealed class PlanFeatureTests(Database database) : IAsyncLifetime
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

    // What exists keeps working: a member who holds a custom role keeps its permissions.
    [Fact]
    public async Task CheckAccess_AMemberWithACustomRoleWhenTheFeatureIsOff_KeepsItsPermissions()
    {
        var (tenantId, _) = await PlanAnswers.FreeTenantAsync(_testData, _api);
        var role = await _testData.AddCustomRoleAsync(tenantId, "Viewer", "tenant.profile.read", "tenant.members.read");
        var member = _api.CreateClient(await _testData.AddMemberWithRoleAsync(tenantId, role));

        var members = await member.GetAsync($"/v1/tenants/{tenantId}/members", Cancellation);

        members.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
