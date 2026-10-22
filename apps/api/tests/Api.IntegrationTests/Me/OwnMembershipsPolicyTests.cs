using Npgsql;

namespace Api.IntegrationTests.Me;

// R11: with no tenant declared and a user declared, the user reads their own memberships in every tenant, and nothing
// else; the extra policy lets them read and never write. Plain SQL as the application account, so only the database decides.
public sealed class OwnMembershipsPolicyTests(Database database)
{
    private readonly TestData _testData = new(database);

    [Fact]
    public async Task ReadMemberships_AsADeclaredUser_ReturnsTheirMembershipsInEveryTenantAndNoOthers()
    {
        var (user, userId) = await UserAsync();
        var first = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(first.Id, user);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user);
        await _testData.AddMemberAsync(first.Id);

        var own = await database.ScalarAsUserAsync<long>(userId, $"SELECT count(*) FROM control_plane.memberships WHERE user_id = '{userId}'");
        var others = await database.ScalarAsUserAsync<long>(userId, $"SELECT count(*) FROM control_plane.memberships WHERE user_id <> '{userId}'");

        own.ShouldBe(2);
        others.ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.memberships WHERE tenant_id = '{first.Id}'")).ShouldBe(2);
    }

    [Fact]
    public async Task InsertMembership_AsADeclaredUser_IsRefused()
    {
        var (_, userId) = await UserAsync();
        var tenant = await _testData.AddTenantAsync();
        var owner = await database.ScalarAsSuperuserAsync<Guid>($"SELECT id FROM control_plane.roles WHERE tenant_id = '{tenant.Id}' AND built_in = 'Owner'");

        var insert = () => database.ExecuteAsUserAsync(
            userId,
            $"INSERT INTO control_plane.memberships (tenant_id, user_id, role_id, joined_at) VALUES ('{tenant.Id}', '{userId}', '{owner}', now())");

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.memberships WHERE user_id = '{userId}'")).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateMembership_AsADeclaredUser_ChangesNoRow()
    {
        var (user, userId) = await UserAsync();
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user, role: "Member");

        var changed = await database.ExecuteAsUserAsync(
            userId,
            $"UPDATE control_plane.memberships SET role_id = (SELECT id FROM control_plane.roles WHERE built_in = 'Owner') WHERE user_id = '{userId}'");

        changed.ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT roles.built_in FROM control_plane.memberships JOIN control_plane.roles ON roles.id = memberships.role_id WHERE user_id = '{userId}'"))
            .ShouldBe("Member");
    }

    [Fact]
    public async Task DeleteMembership_AsADeclaredUser_ChangesNoRow()
    {
        var (user, userId) = await UserAsync();
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user);

        var changed = await database.ExecuteAsUserAsync(userId, $"DELETE FROM control_plane.memberships WHERE user_id = '{userId}'");

        changed.ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.memberships WHERE user_id = '{userId}'")).ShouldBe(1);
    }

    [Fact]
    public async Task ReadMemberships_WithNothingDeclared_ReturnsNoRows()
    {
        var (user, userId) = await UserAsync();
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user);

        var seen = await database.ScalarAsync<long>($"SELECT count(*) FROM control_plane.memberships WHERE user_id = '{userId}'");

        seen.ShouldBe(0);
    }

    private async Task<(string ExternalId, Guid Id)> UserAsync()
    {
        var user = await _testData.AddUserAsync();
        return (user, await _testData.UserIdOfAsync(user));
    }
}
