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
// that the new rule holds. Each module started from one initial migration (step r11a).
public sealed class MigrationStepTests(Database database)
{
    private const string ControlPlaneExists = "SELECT to_regclass('control_plane.tenants') IS NOT NULL";

    private const string MessageStorageExists = "SELECT to_regclass('wolverine.wolverine_incoming_envelopes') IS NOT NULL";

    // A row as text without the columns that a later migration adds, so a test of an earlier migration compares the columns it knew:
    // step 32's tenant plan and the summary's counts of custom roles and pending invitations.
    private const string LaterColumnsLeftOut = "(to_jsonb(row) - ARRAY['plan_id', 'custom_role_count', 'pending_invitation_expiries'])::text";

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
        first.Output.ShouldContain("Migrated the notifications schema:");
        first.Output.ShouldContain("Migrated the api schema:");
        first.Output.ShouldContain("Migration finished: the schemas api, control_plane, notifications, audit and the message storage in wolverine are up to date");
        again.Output.ShouldContain("Migrated the control_plane schema: 0 migrations applied");
        again.Output.ShouldContain("Migrated the audit schema: 0 migrations applied");
        again.Output.ShouldContain("Migrated the notifications schema: 0 migrations applied");
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

    // R15, step r12: the summary of a tenant's Owner invitation holds its language whenever it holds its status. The rows the version
    // before wrote survive: a tenant without an Owner invitation, one with a pending and one with a cancelled Owner invitation. After the
    // migration, a status without a language is refused.
    [Fact]
    public async Task Migrate_RequireOwnerInvitationLanguage_KeepsTheRowsAndRefusesAStatusWithoutALanguage()
    {
        var before = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("RequireOwnerInvitationLanguage");
        var testData = new TestData(database, before, withPlans: false);
        await testData.AddTenantAsync();
        var pending = await testData.AddTenantAsync();
        await testData.AddInvitationAsync(pending.Id, role: "Owner", language: "tr");
        await SummarizeOwnerInvitationAsync(pending.Id, before);
        var cancelled = await testData.AddTenantAsync(status: "Suspended", suspensionReason: "Unpaid invoices");
        await testData.AddInvitationAsync(cancelled.Id, role: "Owner", status: "Cancelled", language: "ar");
        await SummarizeOwnerInvitationAsync(cancelled.Id, before);
        var rowsBefore = await ControlPlaneRowsAsync(before);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, before));

        exitCode.ShouldBe(0, output);
        (await ControlPlaneRowsAsync(before)).ShouldBe(rowsBefore);
        var refused = await Should.ThrowAsync<PostgresException>(() => database.ScalarAsSuperuserAsync<object>(
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_language = NULL WHERE tenant_id = '{pending.Id}'", before));
        refused.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        refused.ConstraintName.ShouldBe("ck_tenant_summaries_owner_invitation_together");
    }

    // Step 32: the migration seeds Free, the default plan, and Pro, which holds what every tenant had (custom roles on, 20 of them, members
    // without a limit), and moves every existing tenant to Pro, so it takes nothing away. The rows the version before wrote survive, apart
    // from the new columns, and each tenant's summary then counts its custom roles and keeps the expiry of each pending invitation.
    [Fact]
    public async Task Migrate_AddPlansAndEntitlements_PutsEveryTenantOnProAndSummarizesItsRolesAndInvitations()
    {
        var before = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("AddPlansAndEntitlements");
        var testData = new TestData(database, before, withPlans: false);
        var active = await testData.AddTenantAsync();
        await testData.AddCustomRoleAsync(active.Id, "Billing", "tenant.profile.read");
        await testData.AddCustomRoleAsync(active.Id, "Support", "tenant.profile.read");
        var expiresAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await testData.AddInvitationAsync(active.Id, expiresAt: expiresAt);
        await testData.AddInvitationAsync(active.Id, status: "Cancelled");
        var closed = await testData.AddTenantAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow);
        var tenantsBefore = await RowsOfAsync(before, "control_plane", "users", "memberships", "roles", "role_permissions", "invitations");

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, before));

        exitCode.ShouldBe(0, output);
        (await RowsOfAsync(before, "control_plane", "users", "memberships", "roles", "role_permissions", "invitations")).ShouldBe(tenantsBefore);
        (await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM control_plane.tenants WHERE id IN ('{active.Id}', '{closed.Id}') AND plan_id = '{SeedPlans.Pro}'", before)).ShouldBe(2);
        (await database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans", before)).ShouldBe(SeedPlans.Free);
        (await database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(key || '=' || coalesce(enabled::text, maximum::text, 'unlimited'), ',' ORDER BY key)
            FROM control_plane.plan_entitlements WHERE plan_id = '{SeedPlans.Pro}'
            """,
            before)).ShouldBe("members.max=unlimited,roles.custom=true,roles.custom.max=20");
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT custom_role_count || ':' || array_to_string(pending_invitation_expiries, ',') FROM control_plane.tenant_summaries WHERE tenant_id = '{active.Id}'",
            before)).ShouldBe("2:2030-01-02 03:04:05+00");
    }

    // Step 33-backend-2: one type for each event, and a person kept by user id. The migration deletes every notification, of any user, as
    // the maintainer decided: no production holds one. A row of an old type is then refused.
    [Fact]
    public async Task Migrate_OneNotificationTypePerEvent_DeletesEveryNotificationAndRefusesAnOldType()
    {
        var before = await database.CreateDatabaseBeforeNotificationsMigrationAsync("OneNotificationTypePerEvent");
        await database.ScalarAsSuperuserAsync<object>(
            $$"""
            INSERT INTO notifications.notifications (id, user_id, tenant_id, type, bell, "values", created_at) VALUES
            ('{{Guid.NewGuid()}}', '{{Guid.NewGuid()}}', '{{Guid.NewGuid()}}', 'tenant.status_changed', 'Platform', '{"tenantName": "Acme", "status": "closed"}', now()),
            ('{{Guid.NewGuid()}}', '{{Guid.NewGuid()}}', '{{Guid.NewGuid()}}', 'invitation.accepted', 'Tenant', '{"tenantName": "Acme", "memberName": "Ada"}', now()),
            ('{{Guid.NewGuid()}}', '{{Guid.NewGuid()}}', NULL, 'member.removed', 'Personal', '{"tenantName": "Acme"}', now())
            """,
            before);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, before));

        exitCode.ShouldBe(0, output);
        (await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM notifications.notifications", before)).ShouldBe(0);
        var refused = await Should.ThrowAsync<PostgresException>(() => database.ScalarAsSuperuserAsync<object>(
            $$"""
            INSERT INTO notifications.notifications (id, user_id, tenant_id, type, bell, "values", created_at)
            VALUES ('{{Guid.NewGuid()}}', '{{Guid.NewGuid()}}', '{{Guid.NewGuid()}}', 'tenant.status_changed', 'Platform', '{}', now())
            """,
            before));
        (refused.SqlState, refused.ConstraintName).ShouldBe((PostgresErrorCodes.CheckViolation, "ck_notifications_type"));
    }

    // Step 33-backend-4: the preferences become a table of categories and channels. The migration copies each stored email choice into the
    // new table: the email's main switch off, and each category whose email is on. The table of the version before keeps its rows, because
    // that version still serves during the deploy (docs/deployment.md, section 8); a later release drops it.
    [Fact]
    public async Task Migrate_NotificationPreferencesPerChannel_KeepsTodaysEmailChoicesAndTheOldRows()
    {
        var before = await database.CreateDatabaseBeforeNotificationsMigrationAsync("NotificationPreferencesPerChannel");
        var (off, chosen, both) = (new Guid("00000000-0000-7000-8000-000000000001"), new Guid("00000000-0000-7000-8000-000000000002"), new Guid("00000000-0000-7000-8000-000000000003"));
        await database.ScalarAsSuperuserAsync<object>(
            $$"""
            INSERT INTO notifications.email_preferences (user_id, enabled, categories) VALUES
            ('{{off}}', false, '{}'),
            ('{{chosen}}', true, '{"access": true, "tenant_plans": true}'),
            ('{{both}}', false, '{"member_invitations": true}')
            """,
            before);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, before));

        exitCode.ShouldBe(0, output);
        (await database.ScalarAsSuperuserAsync<string>(
            "SELECT string_agg(concat_ws(' ', user_id, channels, categories), E'\n' ORDER BY user_id) FROM notifications.notification_preferences",
            before)).ShouldBe(
            $$$"""
            {{{off}}} {"Email": false} {}
            {{{chosen}}} {} {"access": {"Email": true}, "tenant_plans": {"Email": true}}
            {{{both}}} {"Email": false} {"member_invitations": {"Email": true}}
            """);
        (await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM notifications.email_preferences", before)).ShouldBe(3);
    }

    // A tenant always has a plan, and a plan a tenant is on, or the default plan, cannot be deleted (R16). The database holds exactly one
    // default mark (R15).
    [Fact]
    public async Task WritePlans_ARowTheRulesRefuse_IsRefused()
    {
        var name = await database.CreateMigratedDatabaseAsync();

        var deletePlan = await Should.ThrowAsync<PostgresException>(() =>
            database.ScalarAsSuperuserAsync<object>($"DELETE FROM control_plane.plans WHERE id = '{SeedPlans.Free}'", name));
        var secondMark = await Should.ThrowAsync<PostgresException>(() =>
            database.ScalarAsSuperuserAsync<object>($"INSERT INTO control_plane.default_plans (id, plan_id) VALUES (false, '{SeedPlans.Pro}')", name));
        var limitOfAFeature = await Should.ThrowAsync<PostgresException>(() =>
            database.ScalarAsSuperuserAsync<object>(
                $"INSERT INTO control_plane.plan_entitlements (plan_id, key, maximum, unlimited) VALUES ('{SeedPlans.Pro}', 'roles.custom', 3, false)", name));
        var unknownKey = await Should.ThrowAsync<PostgresException>(() =>
            database.ScalarAsSuperuserAsync<object>(
                $"UPDATE control_plane.plan_entitlements SET key = 'projects.max' WHERE plan_id = '{SeedPlans.Pro}' AND key = 'members.max'", name));
        var numberAndUnlimited = await Should.ThrowAsync<PostgresException>(() =>
            database.ScalarAsSuperuserAsync<object>(
                $"UPDATE control_plane.plan_entitlements SET maximum = 3 WHERE plan_id = '{SeedPlans.Pro}' AND key = 'members.max'", name));

        deletePlan.SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);
        secondMark.ConstraintName.ShouldBe("ck_default_plans_one_row");
        limitOfAFeature.ConstraintName.ShouldBe("ck_plan_entitlements_value_of_its_kind");
        unknownKey.ConstraintName.ShouldBe("ck_plan_entitlements_key");
        numberAndUnlimited.ConstraintName.ShouldBe("ck_plan_entitlements_value_of_its_kind");
    }

    // Every row of ControlPlane's tables, as the superuser reads them, as one hash for each table: equal hashes, equal rows.
    private Task<string?> ControlPlaneRowsAsync(string databaseName) =>
        RowsOfAsync(databaseName, "control_plane", "tenants", "tenant_summaries", "users", "memberships", "roles", "role_permissions", "invitations", "system_admins", "tenant_purge_sagas");

    // The summary of the tenant's Owner invitation, as ControlPlane writes it from the invitation (R12).
    private async Task SummarizeOwnerInvitationAsync(Guid tenantId, string databaseName) =>
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            UPDATE control_plane.tenant_summaries AS summary SET owner_invitation_email = invitation.email, owner_invitation_expires_at = invitation.expires_at,
                owner_invitation_status = invitation.status, owner_invitation_language = invitation.language
            FROM control_plane.invitations AS invitation WHERE summary.tenant_id = '{tenantId}' AND invitation.tenant_id = '{tenantId}'
            """,
            databaseName);

    private Task<string?> AuditRowsAsync(string databaseName) => RowsOfAsync(databaseName, "audit", "entries", "platform_entries");

    private Task<string?> RowsOfAsync(string databaseName, string schema, params string[] tables) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT concat_ws(', ', {string.Join(", ", tables.Select(table =>
                $"'{table} ' || (SELECT count(*) || ' ' || md5(coalesce(string_agg({LaterColumnsLeftOut}, ';' ORDER BY {LaterColumnsLeftOut}), '')) FROM {schema}.{table} AS row)"))})",
            databaseName);


    private Task<string?> OwnerOfAsync(string schema, string table, string databaseName) =>
        database.ScalarAsync<string>(
            $"SELECT tableowner FROM pg_tables WHERE schemaname = '{schema}' AND tablename = '{table}'", database: databaseName);
}
