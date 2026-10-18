using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.Users;

// PATCH /v1/platform/users/{userId}: only a system admin changes a user's names, in ControlPlane only (A7). The identity provider
// keeps no name, so it is not told.
public sealed class RenameUserTests(Database database) : IAsyncLifetime
{
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

    // K3: the platform's log records the change, by the system admin who acted, and no name.
    [Fact]
    public async Task RenameUser_NewNames_AnswersTheUserWithThemAndRecordsTheChange()
    {
        var created = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var user = await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Ada", lastName: "Byron", createdAt: created);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user);
        var id = await _testData.UserIdOfAsync(user);
        var admin = await _testData.AddSystemAdminAsync();

        var renamed = await _api.WaitingForMessagesAsync(() => RenameAsync(admin, id, " Ada ", "Lovelace "));

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await renamed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("firstName").GetString(), body.GetProperty("lastName").GetString()).ShouldBe(("Ada", "Lovelace"));
        (body.GetProperty("membershipCount").GetInt32(), body.GetProperty("createdAt").GetDateTimeOffset()).ShouldBe((1, created));
        (await _testData.UserOfAsync(user)).ShouldSatisfyAllConditions(
            stored => stored.FirstName.ShouldBe("Ada"),
            stored => stored.LastName.ShouldBe("Lovelace"));
        (await _audit.PlatformLogAsync(id)).ShouldBe($"user.renamed|User|{await _testData.UserIdOfAsync(admin)}|User|{id}|-");
        _api.Identity.Calls.ShouldBe(0);
    }

    // Renaming sets a state (I1): the names the user has answer as they are, and nothing is recorded.
    [Fact]
    public async Task RenameUser_TheNamesTheUserHas_RecordsNothing()
    {
        var user = await _testData.AddUserAsync(firstName: "Ada", lastName: "Lovelace");
        var id = await _testData.UserIdOfAsync(user);

        var renamed = await _api.WaitingForMessagesAsync(async () => await RenameAsync(await _testData.AddSystemAdminAsync(), id, "Ada", "Lovelace"));

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _audit.PlatformLogAsync(id)).ShouldBeNull();
    }

    [Theory]
    [InlineData("", "required")]
    [InlineData("   ", "required")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "too_long")]
    public async Task RenameUser_WrongFirstName_IsABadRequestForThatField(string firstName, string fieldCode)
    {
        var user = await _testData.AddUserAsync(firstName: "Ada", lastName: "Lovelace");

        var renamed = await RenameAsync(await _testData.AddSystemAdminAsync(), await _testData.UserIdOfAsync(user), firstName, "Lovelace");

        renamed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await renamed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("errors").GetProperty("firstName").EnumerateArray().Select(error => error.GetString()).ShouldBe([fieldCode]);
        (await _testData.UserOfAsync(user)).FirstName.ShouldBe("Ada");
    }

    // The API trims a name before it counts its length, so spaces around a name of the longest length do not make it too long.
    [Fact]
    public async Task RenameUser_LongestNameWithSpacesAround_IsAccepted()
    {
        var longest = new string('a', 100);
        var user = await _testData.AddUserAsync(firstName: "Ada", lastName: "Lovelace");

        var renamed = await RenameAsync(await _testData.AddSystemAdminAsync(), await _testData.UserIdOfAsync(user), $"  {longest}  ", "Lovelace");

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _testData.UserOfAsync(user)).FirstName.ShouldBe(longest);
    }

    // The deletion erased the names; a new one would bring personal data back.
    [Fact]
    public async Task RenameUser_DeletedUser_IsAConflictAndStaysErased()
    {
        var user = await _testData.AddUserAsync(firstName: "Ada", lastName: "Lovelace");
        await _testData.DeleteUserAsync(user);

        var renamed = await RenameAsync(await _testData.AddSystemAdminAsync(), await _testData.UserIdOfAsync(user), "Ada", "King");

        renamed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(renamed)).ShouldBe("user.deleted");
        (await _testData.UserOfAsync(user)).FirstName.ShouldBeNull();
    }

    [Fact]
    public async Task RenameUser_UnknownUser_IsNotFound()
    {
        var renamed = await RenameAsync(await _testData.AddSystemAdminAsync(), Guid.NewGuid(), "Ada", "Lovelace");

        renamed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(renamed)).ShouldBe("user.not_found");
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private Task<HttpResponseMessage> RenameAsync(string admin, Guid userId, string firstName, string lastName) =>
        _api.CreateClient(admin, secondFactor: true).PatchAsJsonAsync($"/v1/platform/users/{userId}", new { firstName, lastName }, Cancellation);
}
