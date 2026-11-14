namespace Api.IntegrationTests.Platform.FeatureFlags;

// DELETE /v1/platform/feature-flags/{key}/tenants/{tenantId}: rule 1 no longer lists the organisation (12.09).
public sealed class RemoveFeatureFlagTenantTests(Database database)
{
    [Fact]
    public async Task RemoveFeatureFlagTenant_AListedOrganisation_TakesItOffAndRecordsIt()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddFeatureFlagTenantAsync("roles.bulk_edit", tenantId));

        var flag = await FlagRequests.JsonAsync(await own.Api.WaitingForMessagesAsync(() => own.Admin.RemoveFeatureFlagTenantAsync("roles.bulk_edit", tenantId)));

        (flag.GetProperty("flag").GetProperty("listedCount").GetInt32(), flag.GetProperty("listed").GetArrayLength(), flag.GetProperty("history")[0].GetProperty("kind").GetString())
            .ShouldBe((0, 0, "unlisted"));
        (await own.ScalarAsync<long>("SELECT count(*) FROM audit.platform_entries WHERE operation = 'feature_flag.tenant_unlisted'")).ShouldBe(1);
    }

    [Fact]
    public async Task RemoveFeatureFlagTenant_NotListed_ChangesAndRecordsNothing()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");
        var (tenantId, _) = await own.Data.AddTenantAsync();

        var flag = await FlagRequests.JsonAsync(await own.Api.WaitingForMessagesAsync(() => own.Admin.RemoveFeatureFlagTenantAsync("roles.bulk_edit", tenantId)));

        flag.GetProperty("history").GetArrayLength().ShouldBe(1);
        (await own.ScalarAsync<long>("SELECT count(*) FROM audit.platform_entries WHERE operation LIKE 'feature_flag.%'")).ShouldBe(0);
    }
}
