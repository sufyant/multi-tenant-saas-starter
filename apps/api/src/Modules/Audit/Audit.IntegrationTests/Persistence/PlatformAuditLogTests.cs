namespace Audit.IntegrationTests.Persistence;

// K3, R11: the platform's audit log has no tenant column. Forced row level security lets only a transaction that declares neither a
// tenant nor a user read it, so no tenant route and no me route can, whatever its SQL. Audit, in a tenant's transaction, still adds the
// platform's entry of a system admin's action on that tenant. Plain SQL as the application account; what is really in the table is
// read as the superuser.
public sealed class PlatformAuditLogTests(Database database)
{
    [Fact]
    public async Task ReadPlatformLog_InATenantTransaction_ReturnsNoRow()
    {
        var entry = await AddEntryAsync();

        var seen = await database.ScalarInTenantAsync<long>(Guid.CreateVersion7(), CountOf(entry));

        seen.ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>(CountOf(entry))).ShouldBe(1);
    }

    [Fact]
    public async Task ReadPlatformLog_InAUserTransaction_ReturnsNoRow()
    {
        var entry = await AddEntryAsync();

        var seen = await database.ScalarAsUserAsync<long>(Guid.CreateVersion7(), CountOf(entry));

        seen.ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>(CountOf(entry))).ShouldBe(1);
    }

    [Fact]
    public async Task AddToPlatformLog_InATenantTransaction_IsWritten()
    {
        var entry = Guid.CreateVersion7();

        var added = await database.ExecuteInTenantAsync(Guid.CreateVersion7(), InsertOf(entry));

        added.ShouldBe(1);
        (await database.ScalarAsSuperuserAsync<long>(CountOf(entry))).ShouldBe(1);
    }

    // The platform door declares nothing.
    [Fact]
    public async Task ReadPlatformLog_WithNothingDeclared_ReturnsTheRows()
    {
        var entry = await AddEntryAsync();

        var seen = await database.ScalarAsync<long>(CountOf(entry));

        seen.ShouldBe(1);
    }

    // The platform's log keeps its actor id as a uuid from the start; the tenant's log does too since step 20f.
    [Fact]
    public async Task ReadPlatformLogColumns_ActorId_IsAUuid()
    {
        var type = await database.ScalarAsSuperuserAsync<string>(
            "SELECT data_type FROM information_schema.columns WHERE table_schema = 'audit' AND table_name = 'platform_entries' AND column_name = 'actor_id'");

        type.ShouldBe("uuid");
    }

    private async Task<Guid> AddEntryAsync()
    {
        var entry = Guid.CreateVersion7();
        await database.ScalarAsSuperuserAsync<object>(InsertOf(entry));
        return entry;
    }

    private static string InsertOf(Guid entry) =>
        $"""
        INSERT INTO audit.platform_entries (id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id)
        VALUES ('{entry}', now(), 'User', '{Guid.CreateVersion7()}', 'Command', 'tenant.suspended', 'Tenant', '{Guid.CreateVersion7()}')
        """;

    private static string CountOf(Guid entry) => $"SELECT count(*) FROM audit.platform_entries WHERE id = '{entry}'";
}
