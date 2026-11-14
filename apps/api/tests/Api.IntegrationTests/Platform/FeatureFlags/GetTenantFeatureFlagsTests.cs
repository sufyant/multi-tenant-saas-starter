using System.Net;

namespace Api.IntegrationTests.Platform.FeatureFlags;

// GET /v1/platform/tenants/{tenantId}/feature-flags: the flags on for one organisation, with the first rule that turns each on (4.09).
public sealed class GetTenantFeatureFlagsTests(Database database)
{
    [Fact]
    public async Task GetTenantFeatureFlags_AListedFlagAndOneOnForEveryone_AnswersBothWithTheirReasons()
    {
        await using var own = await OwnFlags.CreateAsync(database);
        await own.AddFlagAsync("roles.bulk_edit");
        await own.AddFlagAsync("emails.send", type: "KillSwitch", everyoneElse: true);
        await own.AddFlagAsync("search.fuzzy");
        await own.AddFlagAsync("exports.old", everyoneElse: true, removed: true);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddFeatureFlagTenantAsync("roles.bulk_edit", tenantId));

        var flags = await FlagRequests.JsonAsync(await own.Admin.TenantFeatureFlagsAsync(tenantId));

        flags.EnumerateArray().Select(flag => $"{flag.GetProperty("key").GetString()} {flag.GetProperty("type").GetString()} {flag.GetProperty("reason").GetString()}")
            .ShouldBe(["emails.send kill_switch everyone", "roles.bulk_edit release listed"]);
    }

    [Fact]
    public async Task GetTenantFeatureFlags_AnOrganisationThatDoesNotExist_IsNotFound()
    {
        await using var own = await OwnFlags.CreateAsync(database);

        (await own.Admin.TenantFeatureFlagsAsync(Guid.CreateVersion7())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
