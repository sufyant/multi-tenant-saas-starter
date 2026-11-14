using System.Net;

namespace Api.IntegrationTests.Platform.FeatureFlags;

// GET /v1/platform/feature-flags/{key}: one flag with what the code says of it, its rules and its history (12.05, 12.12).
public sealed class GetFeatureFlagTests(Database database)
{
    [Fact]
    public async Task GetFeatureFlag_AKeyNeverStored_IsNotFound()
    {
        await using var own = await OwnFlags.CreateAsync(database);

        var answer = await own.Admin.FeatureFlagAsync("test.unknown");

        (answer.StatusCode, await FlagRequests.CodeOfAsync(answer)).ShouldBe((HttpStatusCode.NotFound, "feature_flag.not_found"));
    }

    [Fact]
    public async Task GetFeatureFlag_AFlagRemovedFromCode_AnswersItWithTheCommitThatRemovedIt()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("exports.old", removed: true);

        var flag = await FlagRequests.JsonAsync(await own.Admin.FeatureFlagAsync("exports.old"));

        (flag.GetProperty("removedRevision").GetString(), flag.GetProperty("addedRevision").GetString(), flag.GetProperty("defaultValue").GetBoolean(),
            flag.GetProperty("history")[0].GetProperty("kind").GetString())
            .ShouldBe(("abc1234", "7c41d09", false, "added_to_code"));
    }
}
