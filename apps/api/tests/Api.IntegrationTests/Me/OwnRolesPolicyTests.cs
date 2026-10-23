using Npgsql;

namespace Api.IntegrationTests.Me;

// R11: with no tenant declared and a user declared, the user reads the roles of their own memberships, in every tenant, and no
// other role; the extra policy lets them read and never write. A user's tenant list and the platform's user detail read the roles
// so. Plain SQL as the application account, so only the database decides.
public sealed class OwnRolesPolicyTests(Database database)
{
    private readonly TestData _testData = new(database);

    [Fact]
    public async Task ReadRoles_AsADeclaredUser_ReturnsTheRolesOfTheirMembershipsAndNoOthers()
    {
        var (user, userId) = await UserAsync();
        var first = await _testData.AddTenantAsync();
        var second = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(first.Id, user, role: "Admin");
        await _testData.AddMemberAsync(second.Id, user, role: "Member");
        await _testData.AddMemberAsync(first.Id, role: "Owner");

        var seen = await database.ScalarAsUserAsync<string>(
            userId,
            $"SELECT string_agg(built_in, ', ' ORDER BY built_in) FROM control_plane.roles WHERE tenant_id IN ('{first.Id}', '{second.Id}')");

        seen.ShouldBe("Admin, Member");
    }

    [Fact]
    public async Task InsertRole_AsADeclaredUser_IsRefused()
    {
        var (user, userId) = await UserAsync();
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id, user);

        var insert = () => database.ExecuteAsUserAsync(
            userId,
            $"INSERT INTO control_plane.roles (id, tenant_id, name) VALUES (gen_random_uuid(), '{tenant.Id}', 'Intruder')");

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.roles WHERE tenant_id = '{tenant.Id}'")).ShouldBe(3);
    }

    [Fact]
    public async Task ChangeRoleRow_AsADeclaredUser_ChangesNoRow()
    {
        var (user, userId) = await UserAsync();
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id, user);

        var changed = await database.ExecuteAsUserAsync(userId, $"UPDATE control_plane.roles SET name = 'renamed' WHERE tenant_id = '{tenant.Id}'");

        changed.ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.roles WHERE name = 'renamed' AND tenant_id = '{tenant.Id}'")).ShouldBe(0);
    }

    private async Task<(string ExternalId, Guid Id)> UserAsync()
    {
        var externalId = await _testData.AddUserAsync();
        return (externalId, await _testData.UserIdOfAsync(externalId));
    }
}
