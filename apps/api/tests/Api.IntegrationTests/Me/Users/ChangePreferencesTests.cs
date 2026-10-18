using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.Users;

// PATCH /v1/me/preferences: the signed-in user sets their language and time zone, which follow them to every tenant and device. It sets
// a state (I1): no idempotency key, and the values the user has change nothing. The API never interprets the time zone: it checks the
// shape of the IANA name only, and keeps it.
public sealed class ChangePreferencesTests(Database database) : IAsyncLifetime
{
    private const string Path = "/v1/me/preferences";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly AuditEntries _audit = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ChangePreferences_NewValues_AnswersAndKeepsThem()
    {
        var user = await _catalog.AddUserAsync(email: "ada@example.com");

        var changed = await _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "tr", timeZone = "Europe/Istanbul" }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preferences = await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        preferences.EnumerateObject().Select(field => (field.Name, field.Value.GetString())).ShouldBe([("language", "tr"), ("timeZone", "Europe/Istanbul")]);
        (await _catalog.UserOfAsync(user)).ShouldBe(new StoredUser("ada@example.com", null, null, Deleted: false, "tr", "Europe/Istanbul"));
    }

    // K3: a command that changes state writes one record. A user belongs to no tenant, so it goes to the platform's log, by the user
    // and about the user. It holds no value: neither the language nor the time zone.
    [Fact]
    public async Task ChangePreferences_NewValues_RecordsTheChangeWithoutTheValues()
    {
        var user = await _catalog.AddUserAsync();
        var userId = await _catalog.UserIdOfAsync(user);

        await _api.WaitingForMessagesAsync(
            () => _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "ar", timeZone = "Asia/Dubai" }, Cancellation));

        (await _audit.PlatformLogAsync(userId)).ShouldBe($"user.preferences_changed|User|{userId}|User|{userId}|-");
        (await _audit.PlatformDetailsAsync(userId)).ShouldBe("-");
        (await _audit.TenantLogAsync(userId)).ShouldBeNull();
    }

    // I1: setting the values the user has answers them, and writes neither the user's row nor an audit entry.
    [Fact]
    public async Task ChangePreferences_TheValuesTheUserHas_AnswersThemAndWritesNothing()
    {
        var user = await _catalog.AddUserAsync(language: "ar", timeZone: "Africa/Cairo");
        var version = await _catalog.UserRowVersionOfAsync(user);

        var changed = await _api.WaitingForMessagesAsync(
            () => _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "ar", timeZone = "Africa/Cairo" }, Cancellation));

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("language").GetString().ShouldBe("ar");
        (await _catalog.UserRowVersionOfAsync(user)).ShouldBe(version);
        (await _audit.PlatformLogAsync(await _catalog.UserIdOfAsync(user))).ShouldBeNull();
    }

    // Null means "not set": the web uses the browser's time zone.
    [Fact]
    public async Task ChangePreferences_NoTimeZone_ClearsIt()
    {
        var user = await _catalog.AddUserAsync(timeZone: "Europe/Dublin");

        var changed = await _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "en", timeZone = (string?)null }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("timeZone").ValueKind.ShouldBe(JsonValueKind.Null);
        (await _catalog.UserOfAsync(user)).TimeZone.ShouldBeNull();
    }

    // Both fields are required, so a client that leaves the time zone out never clears it by mistake. The language is one of the list,
    // by its exact tag: an unknown tag, a list of tags, a tag with spaces or a number is a body the endpoint cannot read, as an unknown
    // role is (K5).
    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "timeZone": null }""")]
    [InlineData("""{ "language": "tr" }""")]
    [InlineData("""{ "language": "fr", "timeZone": null }""")]
    [InlineData("""{ "language": "TR", "timeZone": null }""")]
    [InlineData("""{ "language": 1, "timeZone": null }""")]
    [InlineData("""{ "language": null, "timeZone": null }""")]
    [InlineData("""{ "language": "tr, ar", "timeZone": null }""")]
    [InlineData("""{ "language": "en, tr", "timeZone": null }""")]
    [InlineData("""{ "language": " en", "timeZone": null }""")]
    [InlineData("""{ "language": "", "timeZone": null }""")]
    public async Task ChangePreferences_WithoutALanguageOfTheList_IsABadRequestAndKeepsThePreferences(string body)
    {
        var user = await _catalog.AddUserAsync(language: "ar", timeZone: "Asia/Dubai");

        var changed = await _api.CreateClient(user).PatchAsync(Path, new StringContent(body, Encoding.UTF8, "application/json"), Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(changed)).ShouldBe("request.invalid");
        (await _catalog.UserOfAsync(user)).ShouldBe(new StoredUser(null, null, null, Deleted: false, "ar", "Asia/Dubai"));
    }

    // The shape of an IANA name: parts of letters, digits, "_", "-" and "+", joined by "/" (IANA, "Theory and pragmatics of the tz code
    // and data", "Names of timezones"). The API holds no time zone data, so it checks no more.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Europe/")]
    [InlineData("/Europe/Dublin")]
    [InlineData("Europe//Dublin")]
    [InlineData("Europe Dublin")]
    [InlineData("Europe/Dublin\n")]
    [InlineData("../etc/passwd")]
    [InlineData("Europe/Dübl")]
    [InlineData("+03:00")]
    public async Task ChangePreferences_ATimeZoneOfTheWrongShape_IsABadRequestForTheTimeZone(string timeZone)
    {
        var user = await _catalog.AddUserAsync();

        var changed = await _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "en", timeZone }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldCodesOfAsync(changed)).ShouldBe(("request.validation_failed", "timeZone", "time_zone_invalid"));
        (await _catalog.UserOfAsync(user)).TimeZone.ShouldBeNull();
    }

    // These are shapes the IANA database uses: one part, three parts, digits, "-" and "+".
    [Theory]
    [InlineData("UTC")]
    [InlineData("America/Argentina/ComodRivadavia")]
    [InlineData("America/Port-au-Prince")]
    [InlineData("Etc/GMT+5")]
    [InlineData("Etc/GMT-14")]
    [InlineData("America/North_Dakota/New_Salem")]
    public async Task ChangePreferences_ATimeZoneOfAnIanaShape_IsKept(string timeZone)
    {
        var user = await _catalog.AddUserAsync();

        var changed = await _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "en", timeZone }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _catalog.UserOfAsync(user)).TimeZone.ShouldBe(timeZone);
    }

    [Fact]
    public async Task ChangePreferences_ATimeZoneOverItsLength_IsABadRequestForTheTimeZone()
    {
        var user = await _catalog.AddUserAsync();

        var changed = await _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "en", timeZone = $"Europe/{new string('a', 58)}" }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldCodesOfAsync(changed)).ShouldBe(("request.validation_failed", "timeZone", "too_long"));
    }

    [Fact]
    public async Task ChangePreferences_ATimeZoneOfTheLongestLength_IsKept()
    {
        var user = await _catalog.AddUserAsync();
        var timeZone = $"Europe/{new string('a', 57)}";

        var changed = await _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "en", timeZone }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _catalog.UserOfAsync(user)).TimeZone.ShouldBe(timeZone);
    }

    // A9: a suspended user is refused on every me endpoint.
    [Fact]
    public async Task ChangePreferences_ASuspendedUser_IsForbiddenAndKeepsThePreferences()
    {
        var user = await _catalog.AddUserAsync();
        await _catalog.SuspendUserAsync(user);

        var changed = await _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "tr", timeZone = "Europe/Istanbul" }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(changed)).ShouldBe("user.suspended");
        (await _catalog.UserOfAsync(user)).Language.ShouldBe("en");
    }

    // A8, A9: a deleted user is refused on every me endpoint until their session ends.
    [Fact]
    public async Task ChangePreferences_ADeletedUser_IsForbiddenAndKeepsNoTimeZone()
    {
        var user = await _catalog.AddUserAsync(email: "ada@example.com");
        await _catalog.DeleteUserAsync(user);

        var changed = await _api.CreateClient(user).PatchAsJsonAsync(Path, new { language = "tr", timeZone = "Europe/Istanbul" }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(changed)).ShouldBe("user.deleted");
        (await _catalog.UserOfAsync(user)).TimeZone.ShouldBeNull();
    }

    // A person becomes a catalog user only through an invitation or as the first system admin, as GetMe answers.
    [Fact]
    public async Task ChangePreferences_ASignedInPersonTheCatalogDoesNotKnow_IsNotFound()
    {
        var changed = await _api.CreateClient($"user_{Guid.NewGuid():N}").PatchAsJsonAsync(Path, new { language = "tr", timeZone = (string?)null }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(changed)).ShouldBe("user.not_found");
    }

    [Fact]
    public async Task ChangePreferences_WithoutAToken_IsUnauthorized()
    {
        var changed = await _api.CreateClient().PatchAsJsonAsync(Path, new { language = "tr", timeZone = (string?)null }, Cancellation);

        changed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString();

    // The answer's code, its one failed field and that field's one code.
    private static async Task<(string?, string, string?)> FieldCodesOfAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var field = problem.GetProperty("errors").EnumerateObject().Single();
        return (problem.GetProperty("code").GetString(), field.Name, field.Value.EnumerateArray().Single().GetString());
    }
}
