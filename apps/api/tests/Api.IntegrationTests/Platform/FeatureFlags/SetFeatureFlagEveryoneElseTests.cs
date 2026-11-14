namespace Api.IntegrationTests.Platform.FeatureFlags;

// PUT /v1/platform/feature-flags/{key}/everyone-else: rule 3 turns the flag on or off for everyone else (12.10, 12.11).
public sealed class SetFeatureFlagEveryoneElseTests(Database database)
{
    [Fact]
    public async Task SetFeatureFlagEveryoneElse_AKillSwitchTurnedOff_RecordsItsOwnOperation()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("emails.send", type: "KillSwitch", everyoneElse: true);

        var flag = await FlagRequests.JsonAsync(await own.Api.WaitingForMessagesAsync(() => own.Admin.SetFeatureFlagEveryoneElseAsync("emails.send", false)));

        var change = flag.GetProperty("history")[0];
        (flag.GetProperty("flag").GetProperty("everyoneElse").GetBoolean(), change.GetProperty("from").GetString(), change.GetProperty("to").GetString()).ShouldBe((false, "on", "off"));
        (await own.ScalarAsync<long>("SELECT count(*) FROM audit.platform_entries WHERE operation = 'feature_flag.everyone_else_off'")).ShouldBe(1);
    }
}
