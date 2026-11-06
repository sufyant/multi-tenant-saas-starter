using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Me.Users;

// PATCH /v1/me: the signed-in user corrects their own name (A7), with the rule of the name at an invitation's acceptance: required, 1
// to 100 characters, trimmed. It sets a state (I1): no idempotency key, and the name the user has changes nothing. The platform's
// RenameUser stays.
public sealed class RenameMeTests(Database database) : IAsyncLifetime
{
    private const string Path = "/v1/me";

    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The answer is the user as GetMe reads them, and the name is kept whole, without the spaces around it.
    [Fact]
    public async Task RenameMe_NewName_AnswersMeWithItAndKeepsItTrimmed()
    {
        var user = await _testData.AddUserAsync(email: TestEmails.Of("ada@example.com"), name: "Ada Byron", language: "tr");

        var renamed = await RenameAsync(user, " Ada Lovelace ");

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await renamed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        me.EnumerateObject().Select(field => field.Name)
            .ShouldBe(["id", "email", "name", "isSystemAdmin", "language", "timeZone"], ignoreOrder: true);
        (me.GetProperty("id").GetGuid(), me.GetProperty("name").GetString()).ShouldBe((await _testData.UserIdOfAsync(user), "Ada Lovelace"));
        (me.GetProperty("isSystemAdmin").GetBoolean(), me.GetProperty("language").GetString()).ShouldBe((false, "tr"));
        (await _testData.UserOfAsync(user)).ShouldBe(new StoredUser(TestEmails.Of("ada@example.com"), "Ada Lovelace", Deleted: false, "tr"));
    }

    // K3: the platform's log records the change as user.renamed, by the user and about the user, with no name. A user belongs to no tenant.
    [Fact]
    public async Task RenameMe_NewName_RecordsTheRenameByTheUser()
    {
        var user = await _testData.AddUserAsync(name: "Ada Byron");
        var id = await _testData.UserIdOfAsync(user);

        await _api.WaitingForMessagesAsync(() => RenameAsync(user, "Ada Lovelace"));

        (await _audit.PlatformLogAsync(id)).ShouldBe($"user.renamed|User|{id}|User|{id}|-");
        (await _audit.TenantLogAsync(id)).ShouldBeNull();
    }

    // I1: the name the user has, with or without spaces around it, answers it, and writes neither the user's row nor an audit entry.
    [Theory]
    [InlineData("Ada Lovelace")]
    [InlineData(" Ada Lovelace  ")]
    public async Task RenameMe_TheNameTheUserHas_AnswersItAndWritesNothing(string name)
    {
        var user = await _testData.AddUserAsync(name: "Ada Lovelace");
        var version = await _testData.UserRowVersionOfAsync(user);

        var renamed = await _api.WaitingForMessagesAsync(() => RenameAsync(user, name));

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await renamed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("name").GetString().ShouldBe("Ada Lovelace");
        (await _testData.UserRowVersionOfAsync(user)).ShouldBe(version);
        (await _audit.PlatformLogAsync(await _testData.UserIdOfAsync(user))).ShouldBeNull();
    }

    // The validation of the name at an invitation's acceptance (A7).
    [Theory]
    [InlineData("", "required")]
    [InlineData("   ", "required")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "too_long")]
    public async Task RenameMe_AWrongName_IsABadRequestForTheNameAndKeepsIt(string name, string fieldCode)
    {
        var user = await _testData.AddUserAsync(name: "Ada Lovelace");

        var renamed = await RenameAsync(user, name);

        renamed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldCodesOfAsync(renamed, "name")).ShouldBe(("request.validation_failed", fieldCode));
        (await _testData.UserOfAsync(user)).ShouldBe(new StoredUser(null, "Ada Lovelace", Deleted: false));
    }

    // The name is required, so a client that still sends the first and the last name never clears it by mistake.
    [Fact]
    public async Task RenameMe_TheFirstAndTheLastNameWithoutAName_IsABadRequestForTheName()
    {
        var user = await _testData.AddUserAsync(name: "Ada Lovelace");

        var renamed = await _api.CreateClient(user).PatchAsync(
            Path, new StringContent("""{ "firstName": "Grace", "lastName": "Hopper" }""", Encoding.UTF8, "application/json"), Cancellation);

        renamed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldCodesOfAsync(renamed, "name")).ShouldBe(("request.validation_failed", "required"));
        (await _testData.UserOfAsync(user)).Name.ShouldBe("Ada Lovelace");
    }

    // The API trims a name before it counts its length, so spaces around a name of the longest length do not make it too long.
    [Fact]
    public async Task RenameMe_TheLongestNameWithSpacesAround_IsKept()
    {
        var longest = new string('a', 100);
        var user = await _testData.AddUserAsync(name: "Ada Lovelace");

        var renamed = await RenameAsync(user, $"  {longest}  ");

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _testData.UserOfAsync(user)).Name.ShouldBe(longest);
    }

    // The path names no user: the token decides whose name changes, so a user changes only their own (N6).
    [Fact]
    public async Task RenameMe_AUserIdInTheBody_ChangesOnlyTheCallersName()
    {
        var user = await _testData.AddUserAsync(name: "Ada Lovelace");
        var other = await _testData.AddUserAsync(name: "Grace Hopper");

        var renamed = await _api.CreateClient(user).PatchAsJsonAsync(
            Path, new { userId = await _testData.UserIdOfAsync(other), name = "Ada King" }, Cancellation);

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        ((await _testData.UserOfAsync(user)).Name, (await _testData.UserOfAsync(other)).Name).ShouldBe(("Ada King", "Grace Hopper"));
    }

    // A9: a suspended user is refused on every me endpoint.
    [Fact]
    public async Task RenameMe_ASuspendedUser_IsForbiddenAndKeepsTheName()
    {
        var user = await _testData.AddUserAsync(name: "Ada Lovelace");
        await _testData.SuspendUserAsync(user);

        var renamed = await RenameAsync(user, "Ada King");

        renamed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(renamed)).ShouldBe("user.suspended");
        (await _testData.UserOfAsync(user)).Name.ShouldBe("Ada Lovelace");
    }

    // A8, A9: a deleted user is refused on every me endpoint, so no name comes back after the deletion erased it.
    [Fact]
    public async Task RenameMe_ADeletedUser_IsForbiddenAndStaysErased()
    {
        var user = await _testData.AddUserAsync(name: "Ada Lovelace");
        await _testData.DeleteUserAsync(user);

        var renamed = await RenameAsync(user, "Ada King");

        renamed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(renamed)).ShouldBe("user.deleted");
        (await _testData.UserOfAsync(user)).Name.ShouldBeNull();
    }

    // A person becomes a user only through an invitation or as a root system admin at the system door, as GetMe answers.
    [Fact]
    public async Task RenameMe_ASignedInPersonWhoIsNotAUserYet_IsNotFound()
    {
        var renamed = await RenameAsync($"user_{Guid.NewGuid():N}", "Ada Lovelace");

        renamed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(renamed)).ShouldBe("user.not_found");
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    // The answer's code and the field codes of one field.
    private static async Task<(string? Code, string? FieldCode)> FieldCodesOfAsync(HttpResponseMessage response, string field)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        return (problem.GetProperty("code").GetString(), problem.GetProperty("errors").GetProperty(field).EnumerateArray().Single().GetString());
    }

    private Task<HttpResponseMessage> RenameAsync(string user, string name) =>
        _api.CreateClient(user).PatchAsJsonAsync(Path, new { name }, Cancellation);
}
