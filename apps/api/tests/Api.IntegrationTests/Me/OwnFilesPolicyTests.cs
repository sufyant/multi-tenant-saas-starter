using Npgsql;

namespace Api.IntegrationTests.Me;

// R11: a stored file and a verification code belong to a user (own_files, own_verification_codes). A transaction that declares a user
// reads and writes that user's rows only; one that declares a tenant, or nobody, sees none. Plain SQL as the application account, so only
// the database decides.
public sealed class OwnFilesPolicyTests(Database database)
{
    private readonly TestData _testData = new(database);

    public static TheoryData<string> Tables => ["control_plane.stored_files", "control_plane.verification_codes"];

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task ReadOwnRows_AsADeclaredUser_ReturnsOnlyTheirOwn(string table)
    {
        var (user, other) = (await UserAsync(), await UserAsync());
        await database.ExecuteAsUserAsync(user, InsertSql(table, user));
        await database.ExecuteAsUserAsync(other, InsertSql(table, other));

        var own = await database.ScalarAsUserAsync<long>(user, $"SELECT count(*) FROM {table}");

        own.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task ReadOwnRows_InATenantOrDeclaringNobody_ReturnsNone(string table)
    {
        var user = await UserAsync();
        await database.ExecuteAsUserAsync(user, InsertSql(table, user));

        var inTenant = await database.ScalarInTenantAsync<long>(Guid.CreateVersion7(), $"SELECT count(*) FROM {table} WHERE user_id = '{user}'");
        var declaringNobody = await database.ScalarAsync<long>($"SELECT count(*) FROM {table} WHERE user_id = '{user}'", role: Tenancy.DatabaseRoles.Application);

        (inTenant, declaringNobody).ShouldBe((0, 0));
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task InsertOwnRow_ForAnotherUser_IsRefused(string table)
    {
        var (user, other) = (await UserAsync(), await UserAsync());

        var insert = () => database.ExecuteAsUserAsync(user, InsertSql(table, other));

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task DeleteOwnRows_OfAnotherUser_ChangesNoRow(string table)
    {
        var (user, other) = (await UserAsync(), await UserAsync());
        await database.ExecuteAsUserAsync(other, InsertSql(table, other));

        var deleted = await database.ExecuteAsUserAsync(user, $"DELETE FROM {table}");

        deleted.ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM {table} WHERE user_id = '{other}'")).ShouldBe(1);
    }

    private static string InsertSql(string table, Guid userId) => table == "control_plane.stored_files"
        ? $"""
          INSERT INTO control_plane.stored_files (id, user_id, module, purpose, object_key, content_type, status, created_at)
          VALUES (gen_random_uuid(), '{userId}', 'control_plane', 'DataExport', 'exports/users/{userId}/' || gen_random_uuid() || '.zip', 'application/zip', 'Preparing', now())
          """
        : $"""
          INSERT INTO control_plane.verification_codes (id, user_id, purpose, code_hash, created_at, expires_at, attempts)
          VALUES (gen_random_uuid(), '{userId}', 'AccountDeletion', '\x00', now(), now() + interval '10 minutes', 0)
          """;

    private async Task<Guid> UserAsync() => await _testData.UserIdOfAsync(await _testData.AddUserAsync());
}
