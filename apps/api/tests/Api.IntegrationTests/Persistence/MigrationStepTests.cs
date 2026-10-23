using System.Net;
using System.Text.RegularExpressions;
using Api.Persistence;
using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Persistence;

// Migrations run as a separate step after the bootstrap script, never on application start. A migration runs while the version
// before it still serves, on the rows that version wrote (docs/deployment.md, section 8). So each new migration gets a test here: it
// takes a database back to the migration before it (Database.CreateDatabaseBeforeControlPlaneMigrationAsync, or the Audit one), writes
// rows of every kind the migration touches, migrates, and proves that the rows survive (ControlPlaneRowsAsync, AuditRowsAsync) and
// that the new rule holds. Each module starts from one initial migration (step r11a), so no such test is here yet.
public sealed class MigrationStepTests(Database database)
{
    private const string ControlPlaneExists = "SELECT to_regclass('control_plane.tenants') IS NOT NULL";

    private const string MessageStorageExists = "SELECT to_regclass('wolverine.wolverine_incoming_envelopes') IS NOT NULL";

    // An application whose database was not migrated stops at once, says to run the migration step, and creates nothing on the way.
    [Fact]
    public async Task StartApplication_OnADatabaseNotMigrated_FailsSayingToMigrateAndMigratesNothing()
    {
        var empty = await database.CreateEmptyDatabaseAsync();
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, empty));

        var start = () => api.CreateClient();

        start.ShouldThrow<InvalidOperationException>().Message.ShouldContain("run `dotnet Api.dll migrate`");
        (await database.ScalarAsync<bool>(ControlPlaneExists, database: empty)).ShouldBeFalse();
        (await database.ScalarAsync<bool>(MessageStorageExists, database: empty)).ShouldBeFalse();
    }

    // After a deploy that brings a new migration, every schema is there but a module misses its migration. The application stops at
    // once, names the module and says to run the migration step, rather than fail later on the missing change.
    [Fact]
    public async Task StartApplication_OnADatabaseMissingAModulesLastMigration_FailsNamingTheModuleAndSayingToMigrate()
    {
        var behind = await database.CreateDatabaseWithoutTheLastControlPlaneMigrationAsync();
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, behind));

        var start = () => api.CreateClient();

        var message = start.ShouldThrow<InvalidOperationException>().Message;
        message.ShouldContain("ControlPlane");
        message.ShouldNotContain("Audit");
        message.ShouldContain("run `dotnet Api.dll migrate`");
    }

    [Fact]
    public async Task Migrate_EmptyDatabase_CreatesEveryModuleAndTheMessageStorageAsTheOwner()
    {
        var empty = await database.CreateEmptyDatabaseAsync();

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, empty));

        exitCode.ShouldBe(0, output);
        (await OwnerOfAsync("control_plane", "tenants", empty)).ShouldBe(DatabaseRoles.Owner);
        (await OwnerOfAsync("wolverine", "wolverine_incoming_envelopes", empty)).ShouldBe(DatabaseRoles.Owner);
        (await OwnerOfAsync("wolverine", "wolverine_queue_messages", empty)).ShouldBe(DatabaseRoles.Owner);
        (await OwnerOfAsync("wolverine", "wolverine_queue_messages_scheduled", empty)).ShouldBe(DatabaseRoles.Owner);
    }

    // The step says what it migrated, one line for each module, and that it finished. Run again, it applies nothing new.
    [Fact]
    public async Task Migrate_EmptyDatabaseTwice_LogsEachModuleAndThatItFinished()
    {
        var owner = database.ConnectionStringFor(DatabaseRoles.Owner, await database.CreateEmptyDatabaseAsync());

        var first = await MigrateCommand.RunAsync(owner);
        var again = await MigrateCommand.RunAsync(owner);

        first.ExitCode.ShouldBe(0, first.Output);
        first.Output.ShouldContain("Migrated the control_plane schema:");
        first.Output.ShouldContain("Migrated the audit schema:");
        first.Output.ShouldContain("Migrated the api schema:");
        first.Output.ShouldContain("Migration finished: the schemas api, control_plane, audit and the message storage in wolverine are up to date");
        again.Output.ShouldContain("Migrated the control_plane schema: 0 migrations applied");
        again.Output.ShouldContain("Migrated the audit schema: 0 migrations applied");
        again.Output.ShouldContain("Migrated the api schema: 0 migrations applied");
    }

    // The application role owns nothing the migration step creates, yet it can use all of it, the message storage included.
    [Fact]
    public async Task StartApplication_OnAMigratedDatabase_IsReady()
    {
        var empty = await database.CreateEmptyDatabaseAsync();
        (await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, empty))).ExitCode.ShouldBe(0);
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, empty), ports: HostPorts.Free());

        var ready = await api.ManagementClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);

        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // A failed migration stops the deploy. The process logs the failure once and exits on its own with the code 1, never through the
    // runtime's abort: as PID 1 in a container, an abort can spin forever (Twelve-Factor IX and XII; Nygard, "Fail Fast").
    [Fact]
    public async Task Migrate_WithAWrongPassword_LogsTheFailureOnceAndExitsWithOne()
    {
        var wrongPassword = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString) { Password = "wrong-password" };

        var result = await MigrateCommand.RunWithinTheTimeLimitAsync(wrongPassword.ConnectionString);

        result.Exited.ShouldBeTrue(result.Output);
        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain("28P01: password authentication failed");
        Regex.Count(result.Output, "Migration failed").ShouldBe(1, result.Output);
        result.Output.ShouldNotContain("Unhandled exception");
    }

    [Fact]
    public async Task Migrate_OnAHostThatDoesNotAnswer_LogsTheFailureOnceAndExitsWithOne()
    {
        var result = await MigrateCommand.RunWithinTheTimeLimitAsync(ApiProcess.HostThatDoesNotAnswer);

        result.Exited.ShouldBeTrue(result.Output);
        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain("Failed to connect to 127.0.0.1:1");
        Regex.Count(result.Output, "Migration failed").ShouldBe(1, result.Output);
        result.Output.ShouldNotContain("Unhandled exception");
    }

    // The migration step composes the same host, which reads some settings before it has a logger.
    [Fact]
    public async Task Migrate_WithASettingThatCannotBeRead_WritesTheFailureOnceAndExitsWithOne()
    {
        var result = await ApiProcess.RunAsync(
            new Dictionary<string, string>
            {
                ["ConnectionStrings__Migrations"] = database.OwnerConnectionString,
                ["Host__ManagementPort"] = "abc",
            },
            MigrationStep.Command);

        result.Exited.ShouldBeTrue(result.Output);
        result.ExitCode.ShouldBe(1, result.Output);
        Regex.Count(result.Output, "Failed to convert configuration value 'abc' at 'Host:ManagementPort'").ShouldBe(1, result.Output);
        result.Output.ShouldNotContain("Unhandled exception");
    }

    [Fact]
    public async Task Migrate_WithoutItsOwnConnectionString_Fails()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);

        var migrate = () => MigrationStep.RunAsync(api.Services, TestContext.Current.CancellationToken);

        await migrate.ShouldThrowAsync<InvalidOperationException>();
    }

    // Section 1: each module owns its schema, so no foreign key points from one schema to another.
    [Fact]
    public async Task Migrate_EmptyDatabase_AddsNoForeignKeyAcrossSchemas()
    {
        var migrated = await database.CreateMigratedDatabaseAsync();

        var crossing = await database.ScalarAsSuperuserAsync<string>(
            """
            SELECT coalesce(string_agg(format('%s: %s -> %s', key.conname, key.conrelid::regclass, key.confrelid::regclass), ', '), '')
            FROM pg_constraint AS key
            JOIN pg_class AS source ON source.oid = key.conrelid
            JOIN pg_class AS target ON target.oid = key.confrelid
            WHERE key.contype = 'f' AND source.relnamespace <> target.relnamespace
            """,
            migrated);

        crossing.ShouldBeEmpty();
    }

    // Every row of ControlPlane's tables, as the superuser reads them, as one hash for each table: equal hashes, equal rows.
    private Task<string?> ControlPlaneRowsAsync(string databaseName) =>
        RowsOfAsync(databaseName, "control_plane", "tenants", "tenant_summaries", "users", "memberships", "roles", "role_permissions", "invitations", "system_admins", "tenant_purge_sagas");

    private Task<string?> AuditRowsAsync(string databaseName) => RowsOfAsync(databaseName, "audit", "entries", "platform_entries");

    private Task<string?> RowsOfAsync(string databaseName, string schema, params string[] tables) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT concat_ws(', ', {string.Join(", ", tables.Select(table =>
                $"'{table} ' || (SELECT count(*) || ' ' || md5(coalesce(string_agg(row::text, ';' ORDER BY row::text), '')) FROM {schema}.{table} AS row)"))})",
            databaseName);

    private Task<string?> OwnerOfAsync(string schema, string table, string databaseName) =>
        database.ScalarAsync<string>(
            $"SELECT tableowner FROM pg_tables WHERE schemaname = '{schema}' AND tablename = '{table}'", database: databaseName);
}
