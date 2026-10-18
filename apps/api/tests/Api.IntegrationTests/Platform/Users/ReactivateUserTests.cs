using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.Users;

// POST /v1/platform/users/{userId}/reactivate: a system admin reactivates a suspended catalog user through the system door. The
// identity provider is not told. A test of both a suspension and a reactivation sits here, with the use case it checks last (N9).
public sealed class ReactivateUserTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly AuditEntries _audit = new(database);
    private ApiFactory _api = null!;

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
        var created = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var user = await _catalog.AddUserAsync(createdAt: created);
        await _catalog.SuspendUserAsync(user);

        var response = await (await SystemAdminAsync()).PostAsync($"/v1/platform/users/{await _catalog.UserIdOfAsync(user)}/reactivate", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("status").GetString(), body.GetProperty("membershipCount").GetInt32(), body.GetProperty("createdAt").GetDateTimeOffset())
            .ShouldBe(("active", 0, created));
        (await SuspendedAtAsync(user)).ShouldBeNull();
    }

    [Fact]
    public async Task ReactivateUser_ActiveUser_AnswersTheUserUnchanged()
    {
        var user = await _catalog.AddUserAsync();

        var response = await (await SystemAdminAsync()).PostAsync($"/v1/platform/users/{await _catalog.UserIdOfAsync(user)}/reactivate", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("active");
        (await SuspendedAtAsync(user)).ShouldBeNull();
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task SuspendOrReactivateUser_UnknownUser_IsNotFound(string action)
    {
        var response = await (await SystemAdminAsync()).PostAsync($"/v1/platform/users/{Guid.NewGuid()}/{action}", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("user.not_found");
    }

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private Task<DateTime?> SuspendedAtAsync(string externalId) =>
        database.ScalarAsSuperuserAsync<DateTime?>($"SELECT suspended_at FROM control_plane.users WHERE external_id = '{externalId}'");

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
