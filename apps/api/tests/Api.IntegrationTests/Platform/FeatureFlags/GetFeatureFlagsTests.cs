using System.Text.Json;

namespace Api.IntegrationTests.Platform.FeatureFlags;

// GET /v1/platform/feature-flags: every flag of the environment, with its cleanup, and the counts of the four tabs (12.01 to 12.04).
public sealed class GetFeatureFlagsTests(Database database)
{
    [Fact]
    public async Task GetFeatureFlags_FlagsOfEveryTab_AnswersTheirCleanupAndCountsTheTabs()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit", everyoneElse: true, daysAgo: 40);
        await own.AddFlagAsync("reports.beta", daysAgo: 34);
        await own.AddFlagAsync("emails.send", type: "KillSwitch", everyoneElse: true, daysAgo: 90);
        await own.AddFlagAsync("search.fuzzy");
        await own.AddFlagAsync("exports.old", removed: true);

        var flags = await FlagRequests.JsonAsync(await own.Admin.FeatureFlagsAsync());

        flags.GetProperty("flags").EnumerateArray().Select(flag => $"{flag.GetProperty("key").GetString()} {flag.GetProperty("type").GetString()} {flag.GetProperty("cleanup").GetString()}")
            .ShouldBe(["emails.send kill_switch in_use", "exports.old release in_use", "reports.beta release unused", "roles.bulk_edit release ready_to_remove", "search.fuzzy release in_use"]);
        flags.GetProperty("counts").ToString().ShouldBe("""{"all":4,"readyToRemove":1,"unused":1,"removedFromCode":1}""");
    }

    [Fact]
    public async Task GetFeatureFlags_ADeploysFlag_NamesNoActorInItsLastChange()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");

        var flag = (await FlagRequests.JsonAsync(await own.Admin.FeatureFlagsAsync())).GetProperty("flags")[0];

        flag.GetProperty("lastChange").GetProperty("actor").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
