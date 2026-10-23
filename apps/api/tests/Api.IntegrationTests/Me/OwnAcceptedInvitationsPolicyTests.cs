using Npgsql;

namespace Api.IntegrationTests.Me;

// R11: with no tenant declared and a user declared, the user reads the invitations they accepted, in every tenant, also where no
// membership is left, and no other invitation; the extra policy lets them read and never write. A user's deletion finds the tenants they
// joined so (section 6, "User deletion"). Plain SQL as the application account, so only the database decides.
public sealed class OwnAcceptedInvitationsPolicyTests(Database database)
{
    private readonly TestData _testData = new(database);

    // The two tenants' version 7 ids come from two moments a millisecond apart, so the first is always the lower.
    [Fact]
    public async Task ReadInvitations_AsADeclaredUser_ReturnsTheInvitationsTheyAcceptedAndNoOthers()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: email);
        var userId = await _testData.UserIdOfAsync(user);
        var now = DateTimeOffset.UtcNow;
        var member = await _testData.AddTenantAsync(tenantId: Guid.CreateVersion7(now));
        var left = await _testData.AddTenantAsync(tenantId: Guid.CreateVersion7(now.AddMilliseconds(1)));
        var neverJoined = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(member.Id, user);
        await _testData.AddInvitationAsync(member.Id, email, status: "Accepted", acceptedBy: user);
        await _testData.AddInvitationAsync(left.Id, email, status: "Accepted", acceptedBy: user);
        await _testData.AddInvitationAsync(left.Id, email);
        await _testData.AddInvitationAsync(left.Id, status: "Accepted", acceptedBy: await _testData.AddMemberAsync(left.Id));
        await _testData.AddInvitationAsync(neverJoined.Id, email);
        await _testData.AddInvitationAsync(neverJoined.Id, email, status: "Cancelled");

        var seen = await database.ScalarAsUserAsync<string>(
            userId,
            $"SELECT string_agg(tenant_id::text || ':' || status, ', ' ORDER BY tenant_id) FROM control_plane.invitations");

        seen.ShouldBe($"{member.Id}:Accepted, {left.Id}:Accepted");
    }

    [Fact]
    public async Task InsertInvitation_AsADeclaredUser_IsRefused()
    {
        var user = await _testData.AddUserAsync();
        var userId = await _testData.UserIdOfAsync(user);
        var tenant = await _testData.AddTenantAsync();

        var insert = () => database.ExecuteAsUserAsync(
            userId,
            $"""
            INSERT INTO control_plane.invitations (id, tenant_id, email, role_id, invited_by, created_at, expires_at, secret_hash, status, language)
            SELECT gen_random_uuid(), '{tenant.Id}', 'intruder@example.com', NULL, '{userId}', now(), now(), md5(random()::text), 'Accepted', 'en'
            """);

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.invitations WHERE tenant_id = '{tenant.Id}'")).ShouldBe(0);
    }

    [Fact]
    public async Task ChangeInvitation_AsADeclaredUserWhoAcceptedIt_ChangesNoRow()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: email);
        var userId = await _testData.UserIdOfAsync(user);
        var tenant = await _testData.AddTenantAsync();
        var invitation = await _testData.AddInvitationAsync(tenant.Id, email, status: "Accepted", acceptedBy: user);

        var changed = await database.ExecuteAsUserAsync(userId, $"UPDATE control_plane.invitations SET email = NULL WHERE id = '{invitation}'");
        var deleted = await database.ExecuteAsUserAsync(userId, $"DELETE FROM control_plane.invitations WHERE id = '{invitation}'");

        (changed, deleted).ShouldBe((0, 0));
        (await database.ScalarAsSuperuserAsync<string>($"SELECT email FROM control_plane.invitations WHERE id = '{invitation}'")).ShouldBe(email);
    }
}
