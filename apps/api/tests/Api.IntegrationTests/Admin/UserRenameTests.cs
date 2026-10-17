using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Admin;

// PATCH /v1/admin/users/{userId}: only a system admin changes a user's names, in the catalog only (A7). The identity provider keeps no
// name, so it is not told.
public sealed class UserRenameTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly AuditLogs _audit = new(database);
    private ApiFactory _api = null!;

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
        var user = await _catalog.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Ada", lastName: "Byron");
        var id = await _catalog.UserIdOfAsync(user);
        var admin = await _catalog.AddSystemAdminAsync();

        var renamed = await _api.WaitingForMessagesAsync(() => RenameAsync(admin, id, " Ada ", "Lovelace "));

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await renamed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("firstName").GetString(), body.GetProperty("lastName").GetString()).ShouldBe(("Ada", "Lovelace"));
        (await _catalog.UserOfAsync(user)).ShouldSatisfyAllConditions(
            stored => stored.FirstName.ShouldBe("Ada"),
            stored => stored.LastName.ShouldBe("Lovelace"));
        (await _audit.PlatformLogAsync(id)).ShouldBe($"user.renamed|User|{await _catalog.UserIdOfAsync(admin)}|User|{id}|-");
        _api.Identity.Calls.ShouldBe(0);
    }

    // Renaming sets a state (I1): the names the user has answer as they are, and nothing is recorded.
    [Fact]
    public async Task RenameUser_TheNamesTheUserHas_RecordsNothing()
    {
        var user = await _catalog.AddUserAsync(firstName: "Ada", lastName: "Lovelace");
        var id = await _catalog.UserIdOfAsync(user);

        var renamed = await _api.WaitingForMessagesAsync(async () => await RenameAsync(await _catalog.AddSystemAdminAsync(), id, "Ada", "Lovelace"));

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _audit.PlatformLogAsync(id)).ShouldBeNull();
    }

    [Theory]
    [InlineData("", "required")]
    [InlineData("   ", "required")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "too_long")]
    public async Task RenameUser_WrongFirstName_IsABadRequestForThatField(string firstName, string fieldCode)
    {
        var user = await _catalog.AddUserAsync(firstName: "Ada", lastName: "Lovelace");

        var renamed = await RenameAsync(await _catalog.AddSystemAdminAsync(), await _catalog.UserIdOfAsync(user), firstName, "Lovelace");

        renamed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await renamed.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("errors").GetProperty("firstName").EnumerateArray().Select(error => error.GetString()).ShouldBe([fieldCode]);
        (await _catalog.UserOfAsync(user)).FirstName.ShouldBe("Ada");
    }

    // The deletion erased the names; a new one would bring personal data back.
    [Fact]
    public async Task RenameUser_DeletedUser_IsAConflictAndStaysErased()
    {
        var user = await _catalog.AddUserAsync(firstName: "Ada", lastName: "Lovelace");
        await _catalog.DeleteUserAsync(user);

        var renamed = await RenameAsync(await _catalog.AddSystemAdminAsync(), await _catalog.UserIdOfAsync(user), "Ada", "King");

        renamed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(renamed)).ShouldBe("user.deleted");
        (await _catalog.UserOfAsync(user)).FirstName.ShouldBeNull();
    }

    [Fact]
    public async Task RenameUser_UnknownUser_IsNotFound()
    {
        var renamed = await RenameAsync(await _catalog.AddSystemAdminAsync(), Guid.NewGuid(), "Ada", "Lovelace");

        renamed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(renamed)).ShouldBe("user.not_found");
    }

    private Task<HttpResponseMessage> RenameAsync(string admin, Guid userId, string firstName, string lastName) =>
        _api.CreateClient(admin, secondFactor: true).PatchAsJsonAsync($"/v1/admin/users/{userId}", new { firstName, lastName }, Cancellation);

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
