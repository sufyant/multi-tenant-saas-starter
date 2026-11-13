using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// POST /v1/platform/tenants/{tenantId}/plan/preview: before a tenant changes plan, every key the new plan would put it over, with the
// count and the new value. It never blocks the change.
public sealed class GetTenantPlanChangePreviewTests(Database database) : IAsyncLifetime
{
    private const string Key = PlanAnswers.RetentionKey;

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

    // K14, E4: the new plan's Activity log period against the tenant's at this moment, as ChangeTenantPlan names it in its event. Only a
    // shorter period answers, with the period before (null for no limit); 0 stands for unlimited.
    [Theory]
    [InlineData(365, 90, "365>90")]
    [InlineData(365, 365, "none")]
    [InlineData(365, 730, "none")]
    [InlineData(365, 0, "none")]
    [InlineData(0, 90, "unlimited>90")]
    public async Task GetTenantPlanChangePreview_AnotherActivityLogPeriod_AnswersOnlyAShorterOne(int days, int newDays, string shortened)
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync(planId: await RetentionPlanAsync(days));

        var previewed = await admin.GetTenantPlanChangePreviewAsync(tenant.Id, await RetentionPlanAsync(newDays));

        previewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        PlanAnswers.RetentionShortened(await previewed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).ShouldBe(shortened);
    }

    // E3: an override that has not expired holds the period, so the new plan shortens nothing; one that expired holds nothing.
    [Theory]
    [InlineData(1, "none")]
    [InlineData(-1, "365>90")]
    public async Task GetTenantPlanChangePreview_AnOverrideOfThePeriod_HoldsItOnlyUntilItExpires(int expiresInDays, string shortened)
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync(planId: await RetentionPlanAsync(365));
        await _testData.AddOverrideRowAsync(tenant.Id, Key, 730, DateTimeOffset.UtcNow.AddDays(expiresInDays));

        var previewed = await admin.GetTenantPlanChangePreviewAsync(tenant.Id, await RetentionPlanAsync(90));

        PlanAnswers.RetentionShortened(await previewed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).ShouldBe(shortened);
    }

    private Task<Guid> RetentionPlanAsync(int days) => PlanAnswers.RetentionPlanAsync(_testData, days);

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
