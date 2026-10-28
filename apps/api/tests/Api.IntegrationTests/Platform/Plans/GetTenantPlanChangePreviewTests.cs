using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// POST /v1/platform/tenants/{tenantId}/plan/preview: before a tenant changes plan, every key the new plan would put it over, with the
// count and the new value. It never blocks the change.
public sealed class GetTenantPlanChangePreviewTests(Database database) : IAsyncLifetime
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

    // A Pro tenant with two custom roles and six members, two of them by pending invitations that have not expired, previewed on Free: the
    // feature in use, its limit 0, and five members.
    [Fact]
    public async Task GetTenantPlanChangePreview_ToFree_ListsEachKeyOverWithItsCount()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenantId = await TenantWithRolesMembersAndInvitationsAsync();

        var previewed = await admin.GetTenantPlanChangePreviewAsync(tenantId, SeedPlans.Free);

        previewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await previewed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        preview.GetProperty("excesses").EnumerateArray()
            .Select(excess => $"{excess.GetProperty("key").GetString()}:{excess.GetProperty("count").GetInt32()}:{excess.GetProperty("amount").GetInt32()}")
            .ShouldBe(["members.max:6:1", "roles.custom:2:2", "roles.custom.max:2:2"]);
        preview.GetProperty("overriddenKeys").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetTenantPlanChangePreview_NoPlanWithTheId_IsNotFound()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync();

        var previewed = await admin.GetTenantPlanChangePreviewAsync(tenant.Id, Guid.NewGuid());

        (previewed.StatusCode, (await PlanRequests.ProblemOfAsync(previewed)).Code).ShouldBe((HttpStatusCode.NotFound, "plan.not_found"));
    }

    private async Task<Guid> TenantWithRolesMembersAndInvitationsAsync()
    {
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.profile.read");
        await _testData.AddCustomRoleAsync(tenant.Id, "Support", "tenant.profile.read");
        foreach (var _ in Enumerable.Range(0, 4))
        {
            await _testData.AddMemberAsync(tenant.Id);
        }

        await _testData.AddInvitationAsync(tenant.Id);
        await _testData.AddInvitationAsync(tenant.Id);
        await _testData.AddInvitationAsync(tenant.Id, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        await _testData.SummarizeAsync(tenant.Id);
        return tenant.Id;
    }
}
