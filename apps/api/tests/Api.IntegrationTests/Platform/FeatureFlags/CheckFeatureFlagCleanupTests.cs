namespace Api.IntegrationTests.Platform.FeatureFlags;

// The worker's look at the flags' cleanup, at its start: every system admin hears, in the platform's bell, how many flags are ready to
// remove and how many are unused (3.07). The week's copies tell each of them once.
public sealed class CheckFeatureFlagCleanupTests(Database database)
{
    [Fact]
    public async Task CheckFeatureFlagCleanup_AtTheWorkersStart_TellsEverySystemAdminTheCountsInThePlatformsBell()
    {
        await using var own = await OwnFlags.CreateAsync(database, async name =>
        {
            await OwnFlags.AddFlagAsync(database, name, "roles.bulk_edit", everyoneElse: true, daysAgo: 40);
            await OwnFlags.AddFlagAsync(database, name, "reports.beta", daysAgo: 34);
            await OwnFlags.AddFlagAsync(database, name, "search.old", daysAgo: 31);
        });

        await Waiting.UntilAsync(async () => await own.ScalarAsync<long>("SELECT count(*) FROM notifications.notifications WHERE type LIKE 'feature_flags.%'") == 2);

        (await own.ScalarAsync<string>(
            "SELECT string_agg(type || ' ' || bell || ' ' || (values->>'count'), ', ' ORDER BY type) FROM notifications.notifications WHERE type LIKE 'feature_flags.%'"))
            .ShouldBe("feature_flags.ready_to_remove Platform 1, feature_flags.unused Platform 2");
    }
}
