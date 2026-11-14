using System.Net;

namespace Api.IntegrationTests.Platform.FeatureFlags;

// PUT /v1/platform/feature-flags/{key}/tenants/{tenantId}: rule 1 lists the organisation (12.08).
public sealed class AddFeatureFlagTenantTests(Database database)
{
    [Fact]
    public async Task AddFeatureFlagTenant_AnActiveOrganisation_ListsItAndRecordsItInTheHistoryAndThePlatformsLog()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");
        var (tenantId, _) = await own.Data.AddTenantAsync(name: "Acme Ltd");

        var flag = await FlagRequests.JsonAsync(await own.Api.WaitingForMessagesAsync(() => own.Admin.AddFeatureFlagTenantAsync("roles.bulk_edit", tenantId)));

        (flag.GetProperty("flag").GetProperty("listedCount").GetInt32(), flag.GetProperty("listed")[0].GetProperty("name").GetString(),
            flag.GetProperty("history")[0].GetProperty("kind").GetString(), flag.GetProperty("history")[0].GetProperty("tenant").GetProperty("name").GetString())
            .ShouldBe((1, "Acme Ltd", "listed", "Acme Ltd"));
        (await own.ScalarAsync<string>("SELECT details->>'tenantName' FROM audit.platform_entries WHERE operation = 'feature_flag.tenant_listed'")).ShouldBe("Acme Ltd");
    }

    [Fact]
    public async Task AddFeatureFlagTenant_Twice_ListsOnceAndRecordsOnce()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddFeatureFlagTenantAsync("roles.bulk_edit", tenantId));

        var again = await own.Api.WaitingForMessagesAsync(() => own.Admin.AddFeatureFlagTenantAsync("roles.bulk_edit", tenantId));

        (await FlagRequests.JsonAsync(again)).GetProperty("flag").GetProperty("listedCount").GetInt32().ShouldBe(1);
        (await own.ScalarAsync<long>("SELECT count(*) FROM audit.platform_entries WHERE operation = 'feature_flag.tenant_listed'")).ShouldBe(1);
    }

    [Fact]
    public async Task AddFeatureFlagTenant_AClosedOrganisation_IsNotActiveAndListsNothing()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");
        var (tenantId, _) = await own.Data.AddTenantAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow);

        var answer = await own.Admin.AddFeatureFlagTenantAsync("roles.bulk_edit", tenantId);

        (answer.StatusCode, await FlagRequests.CodeOfAsync(answer)).ShouldBe((HttpStatusCode.Conflict, "tenant.not_active"));
        (await own.ScalarAsync<long>("SELECT count(*) FROM control_plane.feature_flag_tenants")).ShouldBe(0);
    }

    [Fact]
    public async Task AddFeatureFlagTenant_AFlagRemovedFromCode_IsAConflict()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("exports.old", removed: true);
        var (tenantId, _) = await own.Data.AddTenantAsync();

        var answer = await own.Admin.AddFeatureFlagTenantAsync("exports.old", tenantId);

        (answer.StatusCode, await FlagRequests.CodeOfAsync(answer)).ShouldBe((HttpStatusCode.Conflict, "feature_flag.removed_from_code"));
    }

    [Fact]
    public async Task AddFeatureFlagTenant_AnOrganisationThatDoesNotExist_IsNotFound()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");

        var answer = await own.Admin.AddFeatureFlagTenantAsync("roles.bulk_edit", Guid.CreateVersion7());

        (answer.StatusCode, await FlagRequests.CodeOfAsync(answer)).ShouldBe((HttpStatusCode.NotFound, "tenant.not_found"));
    }
}
