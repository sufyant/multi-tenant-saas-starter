using System.Net;
using System.Text.RegularExpressions;
using Api.Persistence;
using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Persistence;

// Migrations run as a separate step after the bootstrap script, never on application start.
public sealed class MigrationStepTests(Database database)
{
    private const string ControlPlaneExists = "SELECT to_regclass('control_plane.tenants') IS NOT NULL";

    private const string SummaryColumnsOfTenants =
        """
        SELECT count(*) FROM information_schema.columns
        WHERE table_schema = 'control_plane' AND table_name = 'tenants' AND (column_name LIKE '%_count' OR column_name LIKE 'owner_invitation_%')
        """;

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

    // R12: the migration that adds the Owner invitation's language to the summary fills it for the tenants that exist, from their own
    // invitations, so the summary still says what the rows say.
    [Fact]
    public async Task Migrate_ATenantWithAPendingOwnerInvitation_FillsItsLanguageInTheSummary()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("AddOwnerInvitationLanguage");
        var testData = new TestData(database, name);
        var summaries = new TenantSummaries(database, name);
        var tenant = await testData.AddTenantRowAsync();
        var invitation = await testData.AddInvitationAsync(tenant.Id, role: "Owner", language: "ar");
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            UPDATE control_plane.tenants AS tenant SET owner_invitation_email = invitation.email,
                owner_invitation_expires_at = invitation.expires_at, owner_invitation_status = invitation.status
            FROM control_plane.invitations AS invitation WHERE tenant.id = '{tenant.Id}' AND invitation.id = '{invitation}'
            """,
            name);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, name));

        exitCode.ShouldBe(0, output);
        (await summaries.StoredAsync(tenant.Id)).ShouldEndWith("|ar");
        await summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // R12: the migration that moves the summary to its own table copies every tenant's summary, then drops the old columns. A tenant with
    // members and one without an Owner, whose summary keeps its Owner invitation, both keep what their summary said.
    [Fact]
    public async Task Migrate_TenantsWithTheirSummaries_MovesEachSummaryToItsOwnTable()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("MoveTenantSummary");
        var testData = new TestData(database, name);
        var summaries = new TenantSummaries(database, name);
        var owned = await testData.AddTenantRowAsync();
        await testData.AddMemberAsync(owned.Id, role: "Owner");
        await testData.AddMemberAsync(owned.Id);
        var invited = await testData.AddTenantRowAsync();
        var invitation = await testData.AddInvitationAsync(invited.Id, role: "Owner", language: "tr");
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            UPDATE control_plane.tenants SET member_count = 2, owner_count = 1 WHERE id = '{owned.Id}';
            UPDATE control_plane.tenants AS tenant SET owner_invitation_email = invitation.email, owner_invitation_expires_at = invitation.expires_at,
                owner_invitation_status = invitation.status, owner_invitation_language = invitation.language
            FROM control_plane.invitations AS invitation WHERE tenant.id = '{invited.Id}' AND invitation.id = '{invitation}'
            """,
            name);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, name));

        exitCode.ShouldBe(0, output);
        (await summaries.StoredAsync(owned.Id)).ShouldBe("2|1|-|-|-|-");
        await summaries.ShouldMatchTheRowsAsync(owned.Id);
        (await summaries.StoredAsync(invited.Id)).ShouldEndWith("|Pending|tr");
        await summaries.ShouldMatchTheRowsAsync(invited.Id);
        (await database.ScalarAsSuperuserAsync<long>(SummaryColumnsOfTenants, name)).ShouldBe(0);
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

    private Task<string?> OwnerOfAsync(string schema, string table, string databaseName) =>
        database.ScalarAsync<string>(
            $"SELECT tableowner FROM pg_tables WHERE schemaname = '{schema}' AND tablename = '{table}'", database: databaseName);
}
