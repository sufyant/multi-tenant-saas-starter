using System.Net;

namespace Api.IntegrationTests.Platform.FeatureFlags;

// POST /v1/platform/feature-flags/{key}/preview: how many organisations one change turns on or off, before it (12.06, 12.08, 12.09).
public sealed class GetFeatureFlagChangePreviewTests(Database database)
{
    [Fact]
    public async Task GetFeatureFlagChangePreview_EveryoneElseOn_CountsTheOrganisationsThatAreNotListed()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");
        var (listed, _) = await own.Data.AddTenantAsync();
        await own.Data.AddTenantAsync();
        await own.Data.AddTenantAsync();
        await own.Data.AddTenantAsync(status: "Purged");
        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddFeatureFlagTenantAsync("roles.bulk_edit", listed));

        var preview = await FlagRequests.JsonAsync(await own.Admin.PreviewFeatureFlagChangeAsync("roles.bulk_edit", new { everyoneElse = true }));

        preview.GetProperty("affectedCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task GetFeatureFlagChangePreview_ListingAnOrganisation_CountsIt()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");
        var (tenantId, _) = await own.Data.AddTenantAsync();

        var preview = await FlagRequests.JsonAsync(await own.Admin.PreviewFeatureFlagChangeAsync("roles.bulk_edit", new { tenantToAdd = tenantId }));

        preview.GetProperty("affectedCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task GetFeatureFlagChangePreview_TwoChanges_IsInvalid()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");

        var answer = await own.Admin.PreviewFeatureFlagChangeAsync("roles.bulk_edit", new { percentage = 10, everyoneElse = true });

        (answer.StatusCode, await FlagRequests.CodeOfAsync(answer)).ShouldBe((HttpStatusCode.BadRequest, "feature_flag.change_invalid"));
    }
}
