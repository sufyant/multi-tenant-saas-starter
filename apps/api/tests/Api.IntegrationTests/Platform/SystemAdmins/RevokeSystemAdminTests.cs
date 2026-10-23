using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.Users;

// DELETE /v1/platform/users/{userId}/system-admin: a system admin takes the grant away from another system admin (section 6, "Root
// system admins"). The user stays a user, with every membership. A root system admin, whom the configuration names, cannot be revoked
// while the list names them, and nobody revokes themself. The platform's log names the system admin who revoked it (K3).
public sealed class RevokeSystemAdminTests(Database database) : IAsyncLifetime
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
    public async Task RevokeSystemAdmin_ASystemAdmin_AnswersNoContentAndKeepsTheUserAndTheirMemberships()
    {
        var tenant = await _testData.AddTenantAsync();
        var systemAdmin = await _testData.AddSystemAdminAsync(email: $"{Guid.NewGuid():N}@example.com");
        await _testData.AddMemberAsync(tenant.Id, systemAdmin, role: "Admin");
        var id = await _testData.UserIdOfAsync(systemAdmin);

        var response = await (await SystemAdminAsync()).DeleteAsync($"/v1/platform/users/{id}/system-admin", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsSystemAdminAsync(systemAdmin)).ShouldBeFalse();
        (await _testData.UserOfAsync(systemAdmin)).Deleted.ShouldBeFalse();
        (await _testData.CountedMembershipsOfAsync(systemAdmin)).ShouldBe(1);
    }

    // The door reads the system admin list on each request (T5), so the revoked admin is refused on the next one.
    [Fact]
    public async Task RevokeSystemAdmin_TheRevokedAdmin_IsRefusedAtTheSystemDoorOnTheNextRequest()
    {
        var systemAdmin = await _testData.AddSystemAdminAsync();
        var client = _api.CreateClient(systemAdmin, secondFactor: true);
        (await client.GetAsync("/v1/platform/users", Cancellation)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await (await SystemAdminAsync()).DeleteAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(systemAdmin)}/system-admin", Cancellation);

        (await client.GetAsync("/v1/platform/users", Cancellation)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RevokeSystemAdmin_ASystemAdmin_IsRecordedInThePlatformLogByTheAdmin()
    {
        var systemAdmin = await _testData.AddSystemAdminAsync();
        var id = await _testData.UserIdOfAsync(systemAdmin);
        var admin = await _testData.AddSystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(admin, secondFactor: true).DeleteAsync($"/v1/platform/users/{id}/system-admin", Cancellation));

        (await _audit.PlatformLogAsync(id)).ShouldBe($"system_admin.revoked|User|{await _testData.UserIdOfAsync(admin)}|User|{id}|-");
    }

    [Fact]
    public async Task RevokeSystemAdmin_ARoot_IsAConflictAndChangesNothing()
    {
        var root = await _testData.AddSystemAdminAsync(email: ApiFactory.RootEmail);

        var response = await (await SystemAdminAsync()).DeleteAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(root)}/system-admin", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("system_admin.root");
        (await IsSystemAdminAsync(root)).ShouldBeTrue();
    }

    // Removing an address from the list lifts the protection only: the person stays a system admin until a system admin revokes them.
    [Fact]
    public async Task RevokeSystemAdmin_ARootAfterTheEmailLeavesTheList_Revokes()
    {
        var root = await _testData.AddSystemAdminAsync(email: ApiFactory.RootEmail);
        var id = await _testData.UserIdOfAsync(root);
        var admin = await _testData.AddSystemAdminAsync();
        (await _api.CreateClient(admin, secondFactor: true).DeleteAsync($"/v1/platform/users/{id}/system-admin", Cancellation)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await using var afterwards = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?> { ["ControlPlane:SystemAdmins:RootEmails:0"] = $"{Guid.NewGuid():N}@example.com" });

        var response = await afterwards.CreateClient(admin, secondFactor: true).DeleteAsync($"/v1/platform/users/{id}/system-admin", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsSystemAdminAsync(root)).ShouldBeFalse();
    }

    // Nobody locks themself out by a click.
    [Fact]
    public async Task RevokeSystemAdmin_Oneself_IsAConflict()
    {
        var admin = await _testData.AddSystemAdminAsync();

        var response = await _api.CreateClient(admin, secondFactor: true).DeleteAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(admin)}/system-admin", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("system_admin.cannot_revoke_self");
        (await IsSystemAdminAsync(admin)).ShouldBeTrue();
    }

    [Fact]
    public async Task RevokeSystemAdmin_AUserWhoIsNoSystemAdmin_IsNotFound()
    {
        var user = await _testData.AddUserAsync();

        var response = await (await SystemAdminAsync()).DeleteAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(user)}/system-admin", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("system_admin.not_found");
    }

    [Fact]
    public async Task RevokeSystemAdmin_AnIdNoUserHas_IsNotFound()
    {
        var response = await (await SystemAdminAsync()).DeleteAsync($"/v1/platform/users/{Guid.NewGuid()}/system-admin", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("user.not_found");
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    private async Task<bool> IsSystemAdminAsync(string externalId) =>
        await database.ScalarAsync<bool>(
            $"SELECT EXISTS (SELECT FROM control_plane.system_admins JOIN control_plane.users ON users.id = system_admins.user_id WHERE users.external_id = '{externalId}')");
}
