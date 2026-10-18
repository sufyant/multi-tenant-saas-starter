using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Contracts.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Platform.Users;

// DELETE /v1/platform/users/{userId}: a system admin deletes a user, never the system by itself. The user leaves every tenant, each in its
// own transaction under the tenant's lock; then ControlPlane erases the user's email address and names (A8), and after that commit Clerk
// deletes its user. A tenant always has at least one Owner (K1), so the tenant's last Owner is never deleted.
public sealed class DeleteUserTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly TenantSummaries _summaries = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // K3: the platform's log records the deletion by the system admin; each tenant's log records that the platform removed the member.
    [Fact]
    public async Task DeleteUser_MemberOfTwoTenants_LeavesBothErasesTheUserAndDeletesTheClerkUser()
    {
        var first = await _testData.AddTenantAsync(tenantId: Guid.CreateVersion7());
        var second = await _testData.AddTenantAsync(tenantId: Guid.CreateVersion7());
        var user = await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Ada", lastName: "Lovelace");
        await _testData.AddMemberAsync(first.Id, user, role: "Member");
        await _testData.AddMemberAsync(second.Id, user, role: "Owner");
        await _testData.AddMemberAsync(second.Id, role: "Owner");
        var id = await _testData.UserIdOfAsync(user);
        var admin = await _testData.AddSystemAdminAsync();

        var deleted = await _api.WaitingForMessagesAsync(() => DeleteAsync(admin, id));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await MembershipsOfAsync(id)).ShouldBe(0);
        (await _testData.UserOfAsync(user)).ShouldBe(new StoredUser(null, null, null, Deleted: true));
        (await _audit.PlatformLogAsync(id)).ShouldBe($"user.deleted|User|{await _testData.UserIdOfAsync(admin)}|User|{id}|-");
        (await _audit.TenantLogAsync(id)).ShouldBe(
            $"{first.Id}:member.removed|Platform|-|User|{id}|-, {second.Id}:member.removed|Platform|-|User|{id}|-");
        _api.Identity.Deleted.ShouldBe([user]);
        await _summaries.ShouldMatchTheRowsAsync(first.Id);
        await _summaries.ShouldMatchTheRowsAsync(second.Id);
    }

    // K3, GDPR Article 17: an audit entry holds no name and no email address, so erasing the user erases them from every
    // audit table, and the read side, through ControlPlane's contract, gives them no name and marks them deleted. The records stay.
    [Fact]
    public async Task DeleteUser_UserNamedInTheAuditLog_LeavesNoNameOrEmailInAnyAuditTableAndReadsAsDeletedUser()
    {
        var marker = Guid.NewGuid().ToString("N");
        var tenant = await _testData.AddTenantAsync();
        var user = await _testData.AddUserAsync(email: $"{marker}@example.com", firstName: $"Ada{marker}", lastName: $"Lovelace{marker}");
        await _testData.AddMemberAsync(tenant.Id, user);
        var userId = await _testData.UserIdOfAsync(user);
        var admin = await _testData.AddSystemAdminAsync();
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(admin, secondFactor: true)
            .PatchAsJsonAsync($"/v1/platform/users/{userId}", new { firstName = $"Grace{marker}", lastName = $"Hopper{marker}" }, Cancellation));
        var before = await NamesAsync(userId);
        var kept = await RowsHoldingAsync("control_plane", marker);

        await _api.WaitingForMessagesAsync(() => DeleteAsync(admin, userId));

        before[userId].ShouldBe(new UserName($"Grace{marker} Hopper{marker}", Deleted: false));
        kept.ShouldBe(1);
        (await RowsHoldingAsync("audit", marker)).ShouldBe(0);
        (await RowsHoldingAsync("control_plane", marker)).ShouldBe(0);
        (await NamesAsync(userId))[userId].ShouldBe(new UserName(null, Deleted: true));
        var adminId = await _testData.UserIdOfAsync(admin);
        (await _audit.PlatformLogAsync(userId)).ShouldBe($"user.renamed|User|{adminId}|User|{userId}|-, user.deleted|User|{adminId}|User|{userId}|-");
    }

    // The check comes first: the tenants where the user is the last Owner are named, and nothing changes anywhere.
    [Fact]
    public async Task DeleteUser_LastOwnerOfATenant_IsAConflictNamingItAndChangesNothing()
    {
        var member = await _testData.AddTenantAsync(tenantId: Guid.CreateVersion7());
        var owned = await _testData.AddTenantAsync(tenantId: Guid.CreateVersion7(), status: "Suspended");
        var user = await _testData.AddUserAsync(firstName: "Ada");
        await _testData.AddMemberAsync(member.Id, user, role: "Member");
        await _testData.AddMemberAsync(owned.Id, user, role: "Owner");
        var id = await _testData.UserIdOfAsync(user);

        var deleted = await _api.WaitingForMessagesAsync(async () => await DeleteAsync(await _testData.AddSystemAdminAsync(), id));

        deleted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await deleted.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("user.last_owner");
        problem.GetProperty("tenantIds").EnumerateArray().Select(tenant => tenant.GetGuid()).ShouldBe([owned.Id]);
        (await MembershipsOfAsync(id)).ShouldBe(2);
        (await _testData.UserOfAsync(user)).ShouldBe(new StoredUser(null, "Ada", null, Deleted: false));
        _api.Identity.Deleted.ShouldBeEmpty();
    }

    // The check passed, but while the deletion ran, the other Owner of the second tenant stepped down. The removal there checks again
    // under the tenant's lock, finds the user its last Owner, and stops the deletion: the user is not erased, and is not deleted at Clerk.
    // The tenant the user already left stays left.
    [Fact]
    public async Task DeleteUser_BecomesTheLastOwnerWhileItRuns_StopsKeepsTheUserAndTheTenantsItLeftStayLeft()
    {
        var left = await _testData.AddTenantAsync(tenantId: Guid.CreateVersion7());
        var owned = await _testData.AddTenantAsync(tenantId: Guid.CreateVersion7());
        var user = await _testData.AddUserAsync(firstName: "Ada");
        await _testData.AddMemberAsync(left.Id, user, role: "Member");
        await _testData.AddMemberAsync(owned.Id, user, role: "Owner");
        var otherOwner = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(owned.Id, role: "Owner"));
        var id = await _testData.UserIdOfAsync(user);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await using var holder = new NpgsqlConnection(database.SuperuserConnectionString);
        await holder.OpenAsync(Cancellation);
        await using var holding = await holder.BeginTransactionAsync(Cancellation);
        await ExecuteAsync(holder, holding, $"SELECT id FROM control_plane.tenants WHERE id = '{owned.Id}' FOR UPDATE");

        var deleting = admin.DeleteAsync($"/v1/platform/users/{id}", Cancellation);
        await Waiting.UntilAsync(async () => deleting.IsCompleted || await WaitingBehindAsync(holder.ProcessID) > 0);
        await ExecuteAsync(
            holder,
            holding,
            $"""
            UPDATE control_plane.memberships SET role_id = (SELECT id FROM control_plane.roles WHERE built_in = 'Member')
            WHERE tenant_id = '{owned.Id}' AND user_id = '{otherOwner}'
            """);
        await holding.CommitAsync(Cancellation);
        var deleted = await deleting;

        deleted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await deleted.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("user.last_owner");
        problem.GetProperty("tenantIds").EnumerateArray().Select(tenant => tenant.GetGuid()).ShouldBe([owned.Id]);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.memberships WHERE user_id = '{id}' AND tenant_id = '{left.Id}'")).ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.memberships WHERE user_id = '{id}' AND tenant_id = '{owned.Id}'")).ShouldBe(1);
        (await _testData.UserOfAsync(user)).ShouldBe(new StoredUser(null, "Ada", null, Deleted: false));
        _api.Identity.Deleted.ShouldBeEmpty();
    }

    // Clerk's deletion runs after ControlPlane's commit, in a handler of its own; a failure there is tried again and never undoes
    // ControlPlane's deletion.
    [Fact]
    public async Task DeleteUser_ClerkFailsOnce_IsTriedAgainAfterTheCommit()
    {
        var user = await _testData.AddUserAsync(firstName: "Ada");
        var id = await _testData.UserIdOfAsync(user);
        _api.Identity.FailNext(1);

        var deleted = await _api.WaitingForMessagesAsync(async () => await DeleteAsync(await _testData.AddSystemAdminAsync(), id));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _api.Identity.Calls.ShouldBe(2);
        _api.Identity.Deleted.ShouldBe([user]);
        (await _testData.UserOfAsync(user)).Deleted.ShouldBeTrue();
    }

    // A deletion sets a state: a second one answers the same and records nothing again; it asks Clerk again, so a Clerk deletion that
    // failed for good can be repeated.
    [Fact]
    public async Task DeleteUser_Twice_AnswersNoContentAndRecordsOnce()
    {
        var user = await _testData.AddUserAsync();
        var id = await _testData.UserIdOfAsync(user);
        var admin = await _testData.AddSystemAdminAsync();
        await _api.WaitingForMessagesAsync(() => DeleteAsync(admin, id));

        var again = await _api.WaitingForMessagesAsync(() => DeleteAsync(admin, id));

        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _audit.PlatformLogAsync(id)).ShouldBe($"user.deleted|User|{await _testData.UserIdOfAsync(admin)}|User|{id}|-");
        _api.Identity.Deleted.ShouldBe([user, user]);
    }

    // A system admin is never deleted, as never suspended: the system door stays open to system admins.
    [Fact]
    public async Task DeleteUser_ASystemAdmin_IsAConflictAndChangesNothing()
    {
        var systemAdmin = await _testData.AddSystemAdminAsync(email: $"{Guid.NewGuid():N}@example.com");

        var deleted = await DeleteAsync(await _testData.AddSystemAdminAsync(), await _testData.UserIdOfAsync(systemAdmin));

        deleted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(deleted)).ShouldBe("user.is_system_admin");
        (await _testData.UserOfAsync(systemAdmin)).Deleted.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteUser_UnknownUser_IsNotFound()
    {
        var deleted = await DeleteAsync(await _testData.AddSystemAdminAsync(), Guid.NewGuid());

        deleted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(deleted)).ShouldBe("user.not_found");
    }

    // Clerk deletes its user only after ControlPlane's commit, and a session token lives until it expires: meanwhile the deleted user is
    // refused on every me and tenant endpoint (A8, A9), so they cannot accept an invitation and become a member again.
    [Fact]
    public async Task DeletedUser_WithASessionStillValid_IsRefused()
    {
        var user = await _testData.AddUserAsync();
        await _testData.DeleteUserAsync(user);

        var me = await _api.CreateClient(user).GetAsync("/v1/me", Cancellation);

        me.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(me)).ShouldBe("user.deleted");
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(Cancellation);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private Task<HttpResponseMessage> DeleteAsync(string admin, Guid userId) =>
        _api.CreateClient(admin, secondFactor: true).DeleteAsync($"/v1/platform/users/{userId}", Cancellation);

    private Task<long> MembershipsOfAsync(Guid userId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.memberships WHERE user_id = '{userId}'");

    private async Task<IReadOnlyDictionary<Guid, UserName>> NamesAsync(Guid userId)
    {
        await using var scope = _api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserDirectory>().FindNamesAsync([userId], Cancellation);
    }

    // The rows of every table in the schema whose text holds the marker, read as the superuser.
    private Task<long> RowsHoldingAsync(string schema, string marker) =>
        database.ScalarAsSuperuserAsync<long>(
            $"""
            SELECT coalesce(sum((xpath('/row/n/text()', query_to_xml(
                format('SELECT count(*) AS n FROM %I.%I r WHERE r::text LIKE %L', schemaname, tablename, '%{marker}%'), false, true, '')))[1]::text::bigint), 0)::bigint
            FROM pg_tables WHERE schemaname = '{schema}'
            """);

    // How many sessions wait for a lock that the holder's session holds.
    private Task<long> WaitingBehindAsync(int holder) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))");
}
