using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform;

// A system admin suspends and reactivates a catalog user through the system door. The identity provider is not told. A system admin
// cannot be suspended, so the system door always stays open to staff. SuspendedUserTests follows what a suspended user meets.
public sealed class UserSuspensionTests(Database database) : IAsyncLifetime
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

    [Fact]
    public async Task SuspendUser_ActiveUser_AnswersTheUserSuspended()
    {
        var user = await _catalog.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Ada", lastName: "Lovelace");
        var id = await _catalog.UserIdOfAsync(user);

        var response = await (await AdminAsync()).PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation);

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
        var admin = await AdminAsync();
        await admin.PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation);
        var first = await SuspendedAtAsync(user);

        var again = await admin.PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("suspended");
        (await SuspendedAtAsync(user)).ShouldBe(first);
    }

    // K3: a user belongs to no tenant, so only the platform's log records it, by the system admin who acted. A repeat changes nothing
    // and is not recorded again.
    [Fact]
    public async Task SuspendAndReactivateUser_EachChange_IsRecordedOnceInThePlatformLogByTheAdmin()
    {
        var user = await _catalog.AddUserAsync();
        var id = await _catalog.UserIdOfAsync(user);
        var admin = await _catalog.AddSystemAdminAsync();
        var client = _api.CreateClient(admin, secondFactor: true);

        await _api.WaitingForMessagesAsync(() => client.PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation));
        await _api.WaitingForMessagesAsync(() => client.PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation));
        await _api.WaitingForMessagesAsync(() => client.PostAsync($"/v1/platform/users/{id}/reactivate", null, Cancellation));
        await _api.WaitingForMessagesAsync(() => client.PostAsync($"/v1/platform/users/{id}/reactivate", null, Cancellation));

        var adminId = await _catalog.UserIdOfAsync(admin);
        (await _audit.PlatformLogAsync(id)).ShouldBe($"user.suspended|User|{adminId}|User|{id}|-, user.reactivated|User|{adminId}|User|{id}|-");
        (await _audit.TenantLogAsync(id)).ShouldBeNull();
    }

    [Fact]
    public async Task ReactivateUser_SuspendedUser_IsActiveAgain()
    {
        var user = await _catalog.AddUserAsync();
        await _catalog.SuspendUserAsync(user);

        var response = await (await AdminAsync()).PostAsync($"/v1/platform/users/{await _catalog.UserIdOfAsync(user)}/reactivate", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("active");
        (await SuspendedAtAsync(user)).ShouldBeNull();
    }

    [Fact]
    public async Task ReactivateUser_ActiveUser_AnswersTheUserUnchanged()
    {
        var user = await _catalog.AddUserAsync();

        var response = await (await AdminAsync()).PostAsync($"/v1/platform/users/{await _catalog.UserIdOfAsync(user)}/reactivate", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("active");
        (await SuspendedAtAsync(user)).ShouldBeNull();
    }

    [Fact]
    public async Task SuspendUser_ASystemAdmin_IsAConflictAndChangesNothing()
    {
        var staff = await _catalog.AddSystemAdminAsync();

        var response = await (await AdminAsync()).PostAsync($"/v1/platform/users/{await _catalog.UserIdOfAsync(staff)}/suspend", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("user.is_system_admin");
        (await SuspendedAtAsync(staff)).ShouldBeNull();
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

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task SuspendOrReactivateUser_UnknownUser_IsNotFound(string action)
    {
        var response = await (await AdminAsync()).PostAsync($"/v1/platform/users/{Guid.NewGuid()}/{action}", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("user.not_found");
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private Task<DateTime?> SuspendedAtAsync(string externalId) =>
        database.ScalarAsSuperuserAsync<DateTime?>($"SELECT suspended_at FROM control_plane.users WHERE external_id = '{externalId}'");

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
