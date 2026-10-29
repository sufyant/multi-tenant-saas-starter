using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Users.EraseFormerMember;
using ControlPlane.Contracts.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tenancy;
using Wolverine;

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
        var (firstId, secondId) = TwoIdsInOrder();
        var first = await _testData.AddTenantAsync(tenantId: firstId);
        var second = await _testData.AddTenantAsync(tenantId: secondId);
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

    // U8: a user's deletion deletes their notifications and their email preferences, in every bell, and leaves another user's.
    [Fact]
    public async Task DeleteUser_WithNotificationsAndPreferences_DeletesThemAndKeepsAnotherUsers()
    {
        var tenant = await _testData.AddTenantAsync();
        var user = await _testData.AddMemberAsync(tenant.Id);
        var id = await _testData.UserIdOfAsync(user);
        var other = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id));
        var rows = new NotificationRows(database);
        await rows.AddAsync(id, "tenant.plan_changed", tenant.Id, "Tenant");
        await rows.AddAsync(id, "member.removed", null, "Personal");
        await rows.AddAsync(other, "tenant.plan_changed", tenant.Id, "Tenant");
        await database.ExecuteAsUserAsync(id, $"INSERT INTO notifications.email_preferences (user_id, enabled, categories) VALUES ('{id}', false, '{{}}')");

        await _api.WaitingForMessagesAsync(async () => await DeleteAsync(await _testData.AddSystemAdminAsync(), id));

        (await rows.OfAsync(id, other)).ShouldBe([NotificationRows.Row(other, "tenant.plan_changed", tenant.Id, "Tenant")]);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM notifications.email_preferences WHERE user_id = '{id}'")).ShouldBe(0);
    }

    // Only the grant and the revocation of a system admin ask for a fresh second factor (decision A of 2026-10-23): a deletion takes a
    // second factor of any age.
    [Fact]
    public async Task DeleteUser_ASecondFactorOlderThanTheReverificationWindow_Deletes()
    {
        var id = await _testData.UserIdOfAsync(await _testData.AddUserAsync());
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new(
            "Bearer",
            TestTokens.WithSecondFactorAge(await _testData.AddSystemAdminAsync(), 60));

        var deleted = await _api.WaitingForMessagesAsync(() => client.DeleteAsync($"/v1/platform/users/{id}", Cancellation));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // A8, GDPR Article 17: an invitation keeps its address as a historical fact, until its invited person is deleted. Each tenant the user
    // leaves erases the address from its invitations the user accepted and from its invitations to the user's address. A tenant that only
    // invited the address, and that the user never joined, keeps it: that is the tenant's data, which goes with the tenant's purge.
    [Fact]
    public async Task DeleteUser_AcceptedInvitationsInTwoTenants_ErasesTheirAddressesAndATenantNeverJoinedKeepsIt()
    {
        var (firstId, secondId) = TwoIdsInOrder();
        var first = await _testData.AddTenantAsync(tenantId: firstId);
        var second = await _testData.AddTenantAsync(tenantId: secondId);
        var neverJoined = await _testData.AddTenantAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: email);
        await _testData.AddMemberAsync(first.Id, user);
        await _testData.AddMemberAsync(second.Id, user);
        await _testData.AddInvitationAsync(first.Id, email, status: "Accepted", acceptedBy: user);
        await _testData.AddInvitationAsync(second.Id, email, status: "Cancelled");
        await _testData.AddInvitationAsync(second.Id, email, status: "Accepted", acceptedBy: user);
        await _testData.AddInvitationAsync(neverJoined.Id, email, status: "Cancelled");
        await _testData.AddInvitationAsync(neverJoined.Id, email);
        var id = await _testData.UserIdOfAsync(user);

        var deleted = await _api.WaitingForMessagesAsync(async () => await DeleteAsync(await _testData.AddSystemAdminAsync(), id));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await InvitationsOfAsync(first.Id)).ShouldBe("Accepted:-");
        (await InvitationsOfAsync(second.Id)).ShouldBe("Accepted:-, Cancelled:-");
        (await InvitationsOfAsync(neverJoined.Id)).ShouldBe($"Cancelled:{email}, Pending:{email}");
        await _summaries.ShouldMatchTheRowsAsync(first.Id);
        await _summaries.ShouldMatchTheRowsAsync(second.Id);
    }

    // The user joined with another of their verified addresses, so a pending invitation to the user's own address stayed in the tenant
    // (member.exists asks the users' addresses). A pending invitation always has an address (ck_invitations_pending_has_email), so the
    // removal cancels it first, expired or not, as a closure does, and withdraws its sign-up. Then it erases both addresses: the user's,
    // and the one of the invitation the user accepted.
    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task DeleteUser_APendingInvitationToTheirAddressInATenantTheyJoined_CancelsItErasesItAndWithdrawsItsSignUp(int days)
    {
        var tenant = await _testData.AddTenantAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        var other = $"{Guid.NewGuid():N}@example.org";
        var user = await _testData.AddUserAsync(email: email);
        await _testData.AddMemberAsync(tenant.Id, user);
        await _testData.AddInvitationAsync(tenant.Id, other, status: "Cancelled");
        await _testData.AddInvitationAsync(tenant.Id, other, status: "Accepted", acceptedBy: user);
        var pending = await _testData.AddInvitationAsync(tenant.Id, email, expiresAt: DateTimeOffset.UtcNow.AddDays(days));
        var signUp = $"inv_{Guid.NewGuid():N}";
        await database.ExecuteInTenantAsync(tenant.Id, $"UPDATE control_plane.invitations SET sign_up_invitation_id = '{signUp}' WHERE id = '{pending}'");
        var id = await _testData.UserIdOfAsync(user);

        var deleted = await _api.WaitingForMessagesAsync(async () => await DeleteAsync(await _testData.AddSystemAdminAsync(), id));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe("Accepted:-, Cancelled:-, Cancelled:-");
        _api.Identity.Withdrawn.ShouldBe([signUp]);
        (await _audit.TenantLogAsync(id)).ShouldBe($"{tenant.Id}:member.removed|Platform|-|User|{id}|-");
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // A8, decision B of 2026-10-23: a tenant the user left before counts as a tenant they joined. Ayşe accepted an invitation in Acme,
    // Acme removed her, and Acme invited her again. Her deletion erases her address from Acme's invitations, cancels the pending one and
    // withdraws its sign-up, as in a tenant she still was a member of. A tenant that only invited her address keeps it.
    [Fact]
    public async Task DeleteUser_AnInvitationAcceptedInATenantTheyLeft_ErasesItsAddressesCancelsThePendingOneAndWithdrawsItsSignUp()
    {
        var acme = await _testData.AddTenantAsync();
        var neverJoined = await _testData.AddTenantAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: email, firstName: "Ayşe");
        await _testData.AddInvitationAsync(acme.Id, email, status: "Accepted", acceptedBy: user);
        var pending = await _testData.AddInvitationAsync(acme.Id, email);
        var signUp = $"inv_{Guid.NewGuid():N}";
        await database.ExecuteInTenantAsync(acme.Id, $"UPDATE control_plane.invitations SET sign_up_invitation_id = '{signUp}' WHERE id = '{pending}'");
        await _testData.SummarizeAsync(acme.Id);
        await _testData.AddInvitationAsync(neverJoined.Id, email);
        var id = await _testData.UserIdOfAsync(user);

        var deleted = await _api.WaitingForMessagesAsync(async () => await DeleteAsync(await _testData.AddSystemAdminAsync(), id));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await InvitationsOfAsync(acme.Id)).ShouldBe("Accepted:-, Cancelled:-");
        (await InvitationsOfAsync(neverJoined.Id)).ShouldBe($"Pending:{email}");
        _api.Identity.Withdrawn.ShouldBe([signUp]);
        _api.Identity.Deleted.ShouldBe([user]);
        await _summaries.ShouldMatchTheRowsAsync(acme.Id);
    }

    // A deletion whose erasure in a tenant the user left did not run, such as a message in the dead letter queue, kept the address there.
    // A second deletion has no address of the user to send, so it erases by the invitation the user accepted: its address goes, and so
    // does a pending one to that address.
    [Fact]
    public async Task DeleteUser_AgainAfterAnEarlierDeletionLeftATenantTheyLeftWithTheAddress_ErasesItThere()
    {
        var tenant = await _testData.AddTenantAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: email);
        await _testData.AddInvitationAsync(tenant.Id, email, status: "Accepted", acceptedBy: user);
        await _testData.AddInvitationAsync(tenant.Id, email);
        await _testData.SummarizeAsync(tenant.Id);
        await _testData.DeleteUserAsync(user);
        var id = await _testData.UserIdOfAsync(user);

        var deleted = await _api.WaitingForMessagesAsync(async () => await DeleteAsync(await _testData.AddSystemAdminAsync(), id));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe("Accepted:-, Cancelled:-");
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // Any message may arrive twice. The second erasure in a tenant the user left finds nothing to cancel or erase, so it withdraws no
    // sign-up again and leaves the rows and the summary as the first left them.
    [Fact]
    public async Task EraseFormerMember_Twice_ChangesNothingMoreAndWithdrawsTheSignUpOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: email);
        await _testData.AddInvitationAsync(tenant.Id, email, status: "Accepted", acceptedBy: user);
        var pending = await _testData.AddInvitationAsync(tenant.Id, email);
        var signUp = $"inv_{Guid.NewGuid():N}";
        await database.ExecuteInTenantAsync(tenant.Id, $"UPDATE control_plane.invitations SET sign_up_invitation_id = '{signUp}' WHERE id = '{pending}'");
        var command = new EraseFormerMemberCommand(await _testData.UserIdOfAsync(user), email);

        await EraseAsync(tenant.Id, command);
        var first = await InvitationsOfAsync(tenant.Id);
        await EraseAsync(tenant.Id, command);

        (first, await InvitationsOfAsync(tenant.Id)).ShouldBe(("Accepted:-, Cancelled:-", "Accepted:-, Cancelled:-"));
        _api.Identity.Withdrawn.ShouldBe([signUp]);
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // Only a finished invitation loses its address: a pending one, expired or not, is still a link to a person (A8).
    [Fact]
    public async Task StoreInvitation_PendingWithoutAnAddress_IsRefused()
    {
        var tenant = await _testData.AddTenantAsync();
        var invitationId = await _testData.AddInvitationAsync(tenant.Id, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var erase = () => database.ExecuteInTenantAsync(tenant.Id, $"UPDATE control_plane.invitations SET email = NULL WHERE id = '{invitationId}'");

        (await erase.ShouldThrowAsync<PostgresException>()).ConstraintName.ShouldBe("ck_invitations_pending_has_email");
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("Cancelled")]
    public async Task StoreInvitation_FinishedWithoutAnAddress_IsStored(string status)
    {
        var tenant = await _testData.AddTenantAsync();
        var invitationId = await _testData.AddInvitationAsync(tenant.Id, status: status);

        await database.ExecuteInTenantAsync(tenant.Id, $"UPDATE control_plane.invitations SET email = NULL WHERE id = '{invitationId}'");

        (await InvitationsOfAsync(tenant.Id)).ShouldBe($"{status}:-");
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
        var (leftId, ownedId) = TwoIdsInOrder();
        var left = await _testData.AddTenantAsync(tenantId: leftId);
        var owned = await _testData.AddTenantAsync(tenantId: ownedId);
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
            UPDATE control_plane.memberships SET role_id = (SELECT id FROM control_plane.roles WHERE tenant_id = '{owned.Id}' AND built_in = 'Member')
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

    // The deletion leaves the user's tenants in the order of their ids. Two version 7 ids of one millisecond differ only in random bits,
    // so their order is random. These ids come from two moments a millisecond apart, so the first is always the lower.
    private static (Guid First, Guid Second) TwoIdsInOrder()
    {
        var now = DateTimeOffset.UtcNow;
        return (Guid.CreateVersion7(now), Guid.CreateVersion7(now.AddMilliseconds(1)));
    }

    // The erasure in the tenant, as a deletion sends it, and every message it causes.
    private Task<Guid> EraseAsync(Guid tenantId, EraseFormerMemberCommand command) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), command, Cancellation);
            return tenantId;
        });

    private Task<HttpResponseMessage> DeleteAsync(string admin, Guid userId) =>
        _api.CreateClient(admin, secondFactor: true).DeleteAsync($"/v1/platform/users/{userId}", Cancellation);

    // The tenant's invitations as the superuser reads them, by status and address: "-" for an erased address.
    private Task<string?> InvitationsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(status || ':' || coalesce(email, '-'), ', ' ORDER BY status, email)
            FROM control_plane.invitations WHERE tenant_id = '{tenantId}'
            """);

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
