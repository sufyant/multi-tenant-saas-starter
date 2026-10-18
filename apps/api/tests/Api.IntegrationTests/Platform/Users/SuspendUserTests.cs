using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.Users;

// POST /v1/platform/users/{userId}/suspend: a system admin suspends a catalog user through the system door. The identity provider is
// not told. A system admin cannot be suspended, so the system door always stays open to system admins. SuspendedUserTests follows what
// a suspended user meets.
public sealed class SuspendUserTests(Database database) : IAsyncLifetime
{
    private readonly Catalog _catalog = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task SuspendUser_ActiveUser_AnswersTheUserSuspended()
    {
        var user = await _catalog.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Ada", lastName: "Lovelace");
        var id = await _catalog.UserIdOfAsync(user);

        var response = await (await SystemAdminAsync()).PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        body.EnumerateObject().Select(field => field.Name)
            .ShouldBe(["id", "email", "firstName", "lastName", "status", "isSystemAdmin", "membershipCount", "createdAt"], ignoreOrder: true);
        body.GetProperty("id").GetGuid().ShouldBe(id);
        body.GetProperty("firstName").GetString().ShouldBe("Ada");
        body.GetProperty("status").GetString().ShouldBe("suspended");
        body.GetProperty("isSystemAdmin").GetBoolean().ShouldBeFalse();
        (await SuspendedAtAsync(user)).ShouldNotBeNull();
    }

    // Suspending sets a state: a repeat answers the same and keeps the time of the first suspension.
    [Fact]
    public async Task SuspendUser_Twice_KeepsTheFirstTime()
    {
        var user = await _catalog.AddUserAsync();
        var id = await _catalog.UserIdOfAsync(user);
        var admin = await SystemAdminAsync();
        await admin.PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation);
        var first = await SuspendedAtAsync(user);

        var again = await admin.PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("suspended");
        (await SuspendedAtAsync(user)).ShouldBe(first);
    }

    [Fact]
    public async Task SuspendUser_ASystemAdmin_IsAConflictAndChangesNothing()
    {
        var systemAdmin = await _catalog.AddSystemAdminAsync();

        var response = await (await SystemAdminAsync()).PostAsync($"/v1/platform/users/{await _catalog.UserIdOfAsync(systemAdmin)}/suspend", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("user.is_system_admin");
        (await SuspendedAtAsync(systemAdmin)).ShouldBeNull();
    }

    // One error code has one text (C7): suspension answers the text that deletion answers.
    [Fact]
    public async Task SuspendUser_ASystemAdmin_AnswersTheTextOfTheCode()
    {
        var systemAdmin = await _catalog.AddSystemAdminAsync();

        var response = await (await SystemAdminAsync()).PostAsync($"/v1/platform/users/{await _catalog.UserIdOfAsync(systemAdmin)}/suspend", null, Cancellation);

        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Detail.ShouldBe("A system admin cannot be suspended or deleted.");
    }

    [Fact]
    public async Task SuspendUser_Oneself_IsAConflict()
    {
        var admin = await _catalog.AddSystemAdminAsync();

        var response = await _api.CreateClient(admin, secondFactor: true)
            .PostAsync($"/v1/platform/users/{await _catalog.UserIdOfAsync(admin)}/suspend", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("user.is_system_admin");
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private Task<DateTime?> SuspendedAtAsync(string externalId) =>
        database.ScalarAsSuperuserAsync<DateTime?>($"SELECT suspended_at FROM control_plane.users WHERE external_id = '{externalId}'");
}
