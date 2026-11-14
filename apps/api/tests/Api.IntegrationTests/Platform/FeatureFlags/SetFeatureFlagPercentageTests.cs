using System.Net;

namespace Api.IntegrationTests.Platform.FeatureFlags;

// PUT /v1/platform/feature-flags/{key}/percentage: rule 2 takes the percentage (12.06).
public sealed class SetFeatureFlagPercentageTests(Database database)
{
    [Fact]
    public async Task SetFeatureFlagPercentage_TenPercent_TakesItAndRecordsTheValuesBeforeAndAfter()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");

        var flag = await FlagRequests.JsonAsync(await own.Api.WaitingForMessagesAsync(() => own.Admin.SetFeatureFlagPercentageAsync("roles.bulk_edit", 10)));

        var change = flag.GetProperty("history")[0];
        (flag.GetProperty("flag").GetProperty("percentage").GetInt32(), change.GetProperty("kind").GetString(), change.GetProperty("from").GetString(), change.GetProperty("to").GetString(),
            change.GetProperty("actor").GetProperty("deleted").GetBoolean())
            .ShouldBe((10, "percentage_changed", "0", "10", false));
        (await own.ScalarAsync<string>("SELECT details::text FROM audit.platform_entries WHERE operation = 'feature_flag.percentage_set'")).ShouldBe("""{"to": 10, "from": 0}""");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task SetFeatureFlagPercentage_OutsideNoughtToAHundred_IsInvalid(int percentage)
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");

        var answer = await own.Admin.SetFeatureFlagPercentageAsync("roles.bulk_edit", percentage);

        (answer.StatusCode, await FlagRequests.CodeOfAsync(answer)).ShouldBe((HttpStatusCode.BadRequest, "feature_flag.percentage_invalid"));
    }
}
