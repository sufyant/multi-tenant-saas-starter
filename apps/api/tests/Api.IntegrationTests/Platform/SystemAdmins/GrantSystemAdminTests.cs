using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.SystemAdmins;

// POST /v1/platform/users/{userId}/system-admin: a system admin makes an existing user a system admin (section 6, "Root system
// admins"). The new system admin enters the system door with a second factor (A6), and the platform's log names the system admin who
// granted it (K3).
public sealed class GrantSystemAdminTests(Database database) : IAsyncLifetime
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

    [Fact]
    public async Task GrantSystemAdmin_AnActiveUser_AnswersTheUserAsASystemAdmin()
    {
        var user = await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Ada", lastName: "Lovelace");
        var id = await _testData.UserIdOfAsync(user);

        var response = await GrantAsync(await SystemAdminAsync(), id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("id").GetGuid(), body.GetProperty("firstName").GetString(), body.GetProperty("status").GetString())
            .ShouldBe((id, "Ada", "active"));
        (body.GetProperty("isSystemAdmin").GetBoolean(), body.GetProperty("isRootSystemAdmin").GetBoolean()).ShouldBe((true, false));
    }

    [Fact]
    public async Task GrantSystemAdmin_TheNewSystemAdmin_EntersTheSystemDoorWithASecondFactor()
    {
        var user = await _testData.AddUserAsync();

        await GrantAsync(await SystemAdminAsync(), await _testData.UserIdOfAsync(user));

        (await _api.CreateClient(user, secondFactor: true).GetAsync("/v1/platform/users", Cancellation)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.CreateClient(user, secondFactor: false).GetAsync("/v1/platform/users", Cancellation)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GrantSystemAdmin_AnActiveUser_IsRecordedInThePlatformLogByTheAdmin()
    {
        var user = await _testData.AddUserAsync();
        var id = await _testData.UserIdOfAsync(user);
        var admin = await _testData.AddSystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => GrantAsync(_api.CreateClient(admin, secondFactor: true), id));

        (await _audit.PlatformLogAsync(id)).ShouldBe($"system_admin.granted|User|{await _testData.UserIdOfAsync(admin)}|User|{id}|-");
        (await _audit.TenantLogAsync(id)).ShouldBeNull();
    }

    // I1, I5: a retry with the same key replays the first answer, and the grant is written and recorded once.
    [Fact]
    public async Task GrantSystemAdmin_TheSameKeyTwice_ReplaysTheFirstAnswerAndRecordsOnce()
    {
        var user = await _testData.AddUserAsync();
        var id = await _testData.UserIdOfAsync(user);
        var client = await SystemAdminAsync();
        var key = Guid.NewGuid().ToString();

        var first = await _api.WaitingForMessagesAsync(() => GrantAsync(client, id, key));
        var again = await _api.WaitingForMessagesAsync(() => GrantAsync(client, id, key));

        (first.StatusCode, again.StatusCode).ShouldBe((HttpStatusCode.Created, HttpStatusCode.Created));
        again.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await _audit.PlatformLogAsync(id))!.Split(", ").Length.ShouldBe(1);
    }

    [Fact]
    public async Task GrantSystemAdmin_WithoutAKey_IsABadRequestAndChangesNothing()
    {
        var user = await _testData.AddUserAsync();

        var response = await (await SystemAdminAsync()).PostAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(user)}/system-admin", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await IsSystemAdminAsync(user)).ShouldBeFalse();
    }

    // A grant is not a state to set again: the second answers 409 and writes no second entry.
    // With a new key, a grant of a system admin is a new request: it answers 409 and writes no second entry.
    [Fact]
    public async Task GrantSystemAdmin_ASystemAdmin_IsAConflictAndRecordsNothing()
    {
        var systemAdmin = await _testData.AddSystemAdminAsync();
        var id = await _testData.UserIdOfAsync(systemAdmin);

        var response = await _api.WaitingForMessagesAsync(async () => await GrantAsync(await SystemAdminAsync(), id));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("system_admin.exists");
        (await _audit.PlatformLogAsync(id)).ShouldBeNull();
    }

    [Fact]
    public async Task GrantSystemAdmin_ASuspendedUser_IsAConflictAndChangesNothing()
    {
        var user = await _testData.AddUserAsync();
        await _testData.SuspendUserAsync(user);

        var response = await GrantAsync(await SystemAdminAsync(), await _testData.UserIdOfAsync(user));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("user.suspended");
        (await IsSystemAdminAsync(user)).ShouldBeFalse();
    }

    [Fact]
    public async Task GrantSystemAdmin_ADeletedUser_IsAConflictAndChangesNothing()
    {
        var user = await _testData.AddUserAsync();
        await _testData.DeleteUserAsync(user);

        var response = await GrantAsync(await SystemAdminAsync(), await _testData.UserIdOfAsync(user));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("user.deleted");
        (await IsSystemAdminAsync(user)).ShouldBeFalse();
    }

    [Fact]
    public async Task GrantSystemAdmin_AnIdNoUserHas_IsNotFound()
    {
        var response = await GrantAsync(await SystemAdminAsync(), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("user.not_found");
    }

    [Fact]
    public async Task GrantSystemAdmin_AsATenantOwner_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var user = await _testData.AddUserAsync();

        var response = await GrantAsync(_api.CreateClient(owner, secondFactor: true), await _testData.UserIdOfAsync(user));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await IsSystemAdminAsync(user)).ShouldBeFalse();
    }

    // A grant with an idempotency key (I1): the given one, or a new one.
    private static Task<HttpResponseMessage> GrantAsync(HttpClient client, Guid userId, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/platform/users/{userId}/system-admin");
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return client.SendAsync(request, Cancellation);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    private async Task<bool> IsSystemAdminAsync(string externalId) =>
        await database.ScalarAsync<bool>(
            $"SELECT EXISTS (SELECT FROM control_plane.system_admins JOIN control_plane.users ON users.id = system_admins.user_id WHERE users.external_id = '{externalId}')");
}
