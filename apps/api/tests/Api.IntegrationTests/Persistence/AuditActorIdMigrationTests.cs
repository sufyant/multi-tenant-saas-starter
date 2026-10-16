namespace Api.IntegrationTests.Persistence;

// Step 20f: the tenant's audit log keeps the actor id as a uuid, as the platform's log does (K3). The migration changes the column in
// place, with a USING cast; a record the previous version wrote as text reads back as the same uuid. Each test has a database of its own,
// because it needs the schema before the migration.
public sealed class AuditActorIdMigrationTests(Database database)
{
    [Fact]
    public async Task MigrateAuditLog_AnActorIdWrittenAsText_ReadsBackAsTheSameUuid()
    {
        var name = await database.CreateDatabaseBeforeAuditMigrationAsync(Migration);
        var (entry, actor) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, actor_id, kind, operation)
            VALUES ('{entry}', '{Guid.CreateVersion7()}', now(), 'User', '{actor}', 'Command', 'member.removed')
            """,
            name);

        await database.MigrateDatabaseAsync(name);

        (await database.ScalarAsSuperuserAsync<Guid>($"SELECT actor_id FROM audit.entries WHERE id = '{entry}'", name)).ShouldBe(actor);
        (await database.ScalarAsSuperuserAsync<string>(ActorIdType, name)).ShouldBe("uuid");
    }

    // The cast is the check: a value that is not a uuid, such as an identity provider's id, stops the migration, and its transaction
    // leaves the column as it was.
    [Fact]
    public async Task MigrateAuditLog_AnActorIdThatIsNotAUuid_StopsTheMigrationAndChangesNothing()
    {
        var name = await database.CreateDatabaseBeforeAuditMigrationAsync(Migration);
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_id, kind, operation)
            VALUES ('{Guid.CreateVersion7()}', '{Guid.CreateVersion7()}', now(), 'user_2abc', 'Command', 'tenant.created')
            """,
            name);

        await Should.ThrowAsync<Exception>(() => database.MigrateDatabaseAsync(name));

        (await database.ScalarAsSuperuserAsync<string>(ActorIdType, name)).ShouldBe("character varying");
    }

    private const string Migration = "20261016201929_ActorIdUuidAndCursorIndexes";

    private const string ActorIdType =
        "SELECT data_type FROM information_schema.columns WHERE table_schema = 'audit' AND table_name = 'entries' AND column_name = 'actor_id'";
}
