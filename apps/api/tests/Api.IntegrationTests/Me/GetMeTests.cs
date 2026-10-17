using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Me;

// GET /v1/me: the signed-in user, as the catalog knows them. A person becomes a catalog user only through an invitation or as the
// first system admin, so a signed-in person the catalog does not know is not found, and the web shows "no access yet".
public sealed class GetMeTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task GetMe_AUser_ReturnsTheirCatalogIdProfileAndThatTheyAreNoSystemAdmin()
    {
        var user = await _catalog.AddUserAsync(email: "ada@example.com", firstName: "Ada", lastName: "Lovelace");

        var response = await _api.CreateClient(user).GetAsync("/v1/me", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Me(me).ShouldBe((await _catalog.UserIdOfAsync(user), "ada@example.com", "Ada", "Lovelace", false));
        me.EnumerateObject().Select(field => field.Name).ShouldBe(
            ["id", "email", "firstName", "lastName", "isSystemAdmin", "language", "timeZone"], ignoreOrder: true);
    }

    // The preferences follow the user to every tenant and device. A user who set no time zone has none: the web uses the browser's.
    [Theory]
    [InlineData(null, null, "en", null)]
    [InlineData("ar", "Asia/Dubai", "ar", "Asia/Dubai")]
    [InlineData("tr", null, "tr", null)]
    public async Task GetMe_AUser_AnswersTheirLanguageAndTimeZone(string? language, string? timeZone, string answeredLanguage, string? answeredTimeZone)
    {
        var user = await _catalog.AddUserAsync(language: language, timeZone: timeZone);

        var me = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>("/v1/me", Cancellation);

        (me.GetProperty("language").GetString(), me.GetProperty("timeZone").GetString()).ShouldBe((answeredLanguage, answeredTimeZone));
    }

    // The me group checks no second factor: a system admin reads their own profile like any user.
    [Fact]
    public async Task GetMe_ASystemAdmin_SaysTheyAreOne()
    {
        var admin = await _catalog.AddSystemAdminAsync(email: "grace@example.com");

        var me = await _api.CreateClient(admin).GetFromJsonAsync<JsonElement>("/v1/me", Cancellation);

        Me(me).ShouldBe((await _catalog.UserIdOfAsync(admin), "grace@example.com", null, null, true));
    }

    [Fact]
    public async Task GetMe_ASignedInPersonTheCatalogDoesNotKnow_IsNotFound()
    {
        var response = await _api.CreateClient($"user_{Guid.NewGuid():N}").GetAsync("/v1/me", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("user.not_found");
    }

    [Fact]
    public async Task GetMe_ASuspendedUser_IsForbiddenAndToldWhy()
    {
        var user = await _catalog.AddUserAsync(email: "sus@example.com");
        await _catalog.SuspendUserAsync(user);

        var response = await _api.CreateClient(user).GetAsync("/v1/me", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("user.suspended");
    }

    [Fact]
    public async Task GetMe_WithoutAToken_IsUnauthorized()
    {
        var response = await _api.CreateClient().GetAsync("/v1/me", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static (Guid Id, string? Email, string? FirstName, string? LastName, bool IsSystemAdmin) Me(JsonElement me) => (
        me.GetProperty("id").GetGuid(),
        me.GetProperty("email").GetString(),
        me.GetProperty("firstName").GetString(),
        me.GetProperty("lastName").GetString(),
        me.GetProperty("isSystemAdmin").GetBoolean());
}
