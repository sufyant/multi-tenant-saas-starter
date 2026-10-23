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

    private const string PendingEmailIndex =
        "SELECT indexdef FROM pg_indexes WHERE schemaname = 'control_plane' AND indexname = 'ix_invitations_pending_email'";

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

    // The migration that keeps one form of an address lowercases the stored addresses in the database, and the form it leaves is the one
    // .NET gives (EmailAddressTests): it keeps the Turkish dotted capital I, as .NET does, where PostgreSQL's lower() alone would not. The unique index of pending invitations is then on the
    // stored value, without lower() (the maintainer's decision in step r2a).
    [Fact]
    public async Task Migrate_AddressesInMixedCase_KeepsThemInLowerCaseAndIndexesTheStoredValue()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("KeepEmailAddressesInLowerCase");
        var testData = new TestData(database, name);
        var local = Guid.NewGuid().ToString("N");
        var user = await testData.AddUserAsync(email: $"Ali.{local}@Example.COM");
        var tenant = await testData.AddTenantAsync();
        var invitation = await testData.AddInvitationAsync(tenant.Id, $"İpek.{local}@Example.com", role: "Owner");
        await SummarizeOwnerInvitationAsync(tenant.Id, name);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, name));

        exitCode.ShouldBe(0, output);
        (await testData.UserOfAsync(user, name)).Email.ShouldBe($"ali.{local}@example.com");
        (await database.ScalarAsSuperuserAsync<string>($"SELECT email FROM control_plane.invitations WHERE id = '{invitation}'", name))
            .ShouldBe($"İpek.{local}@example.com");
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT owner_invitation_email FROM control_plane.tenant_summaries WHERE tenant_id = '{tenant.Id}'", name)).ShouldBe($"İpek.{local}@example.com");
        (await database.ScalarAsSuperuserAsync<string>(PendingEmailIndex, name)).ShouldBe(
            "CREATE UNIQUE INDEX ix_invitations_pending_email ON control_plane.invitations USING btree (tenant_id, email) WHERE ((status)::text = 'Pending'::text)");
    }

    // Tenant creation is one transaction (section 6, "Tenant creation process"). The migration that removes the creation saga makes a
    // provisioning tenant active and a cancelled one closed at the migration's time, so it can be reopened; an active tenant stays as it
    // is. It drops the saga's table and the cancellation reason.
    [Fact]
    public async Task Migrate_ProvisioningAndCancelledTenants_BecomeActiveAndClosedAndTheSagaTableGoes()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("RemoveTenantCreationSaga");
        var testData = new TestData(database, name);
        var provisioning = await testData.AddTenantAsync(status: "Provisioning");
        var cancelled = await testData.AddTenantAsync(status: "Cancelled");
        var active = await testData.AddTenantAsync();
        var started = await database.ScalarAsSuperuserAsync<DateTime>("SELECT now()", name);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, name));

        exitCode.ShouldBe(0, output);
        (await database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(format('%s|%s', status, CASE WHEN closed_at IS NULL THEN '-' WHEN closed_at >= '{started:O}'::timestamptz THEN 'now' ELSE 'before' END), ', '
                ORDER BY array_position(ARRAY['{provisioning.Id}', '{cancelled.Id}', '{active.Id}']::uuid[], id))
            FROM control_plane.tenants WHERE id IN ('{provisioning.Id}', '{cancelled.Id}', '{active.Id}')
            """,
            name)).ShouldBe("Active|-, Closed|now, Active|-");
        (await database.ScalarAsSuperuserAsync<bool>("SELECT to_regclass('control_plane.tenant_creation_sagas') IS NULL", name)).ShouldBeTrue();
        (await database.ScalarAsSuperuserAsync<long>(
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'control_plane' AND table_name = 'tenants' AND column_name = 'cancellation_reason'",
            name)).ShouldBe(0);
    }

    // A2: the migration that gives each tenant its roles writes every tenant's Owner, Admin and Member rows, moves each membership and
    // invitation to its own tenant's role of the same kind, and removes the roles of no tenant. The composite foreign key then refuses a
    // role of another tenant, also to the superuser, whom row level security does not hold (Citus documentation, "Multi-tenant
    // applications").
    [Fact]
    public async Task Migrate_TwoTenantsWithMembersAndInvitations_KeepEveryRoleAsARoleOfTheirOwnTenant()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("GiveEachTenantItsRoles");
        var testData = new TestData(database, name);
        var first = await testData.AddTenantAsync();
        var second = await testData.AddTenantAsync();
        await testData.AddMemberAsync(first.Id, role: "Owner");
        await testData.AddMemberAsync(first.Id, role: "Admin");
        await testData.AddInvitationAsync(first.Id, role: "Member");
        await testData.AddMemberAsync(second.Id, role: "Member");
        await testData.AddInvitationAsync(second.Id, role: "Owner");
        await testData.AddInvitationAsync(second.Id, role: "Admin");

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, name));

        exitCode.ShouldBe(0, output);
        (await RolesOfAsync(first.Id, name)).ShouldBe("Admin|admin, Member|member, Owner|owner");
        (await RolesOfAsync(second.Id, name)).ShouldBe("Admin|admin, Member|member, Owner|owner");
        (await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM control_plane.roles", name)).ShouldBe(6);
        (await RolesOfOwnTenantAsync("memberships", first.Id, name)).ShouldBe("Admin, Owner");
        (await RolesOfOwnTenantAsync("invitations", first.Id, name)).ShouldBe("Member");
        (await RolesOfOwnTenantAsync("memberships", second.Id, name)).ShouldBe("Member");
        (await RolesOfOwnTenantAsync("invitations", second.Id, name)).ShouldBe("Admin, Owner");
        var crossing = () => database.ScalarAsSuperuserAsync<object>(
            $"""
            UPDATE control_plane.memberships SET role_id = (SELECT id FROM control_plane.roles WHERE tenant_id = '{second.Id}' AND built_in = 'Owner')
            WHERE tenant_id = '{first.Id}'
            """,
            name);
        (await crossing.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    // R15: the migration that adds ControlPlane's CHECKs keeps every row the code writes: tenants of each kind with their summaries, one
    // with an Owner invitation, users active, suspended and deleted, invitations of each status, a purge's record. From then on the database
    // refuses each row that breaks a rule, and names the rule.
    [Fact]
    public async Task Migrate_ControlPlaneRowsOfEveryKind_KeepsThemAndRefusesEachRowThatBreaksACheck()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("AddCheckConstraints");
        var testData = new TestData(database, name);
        var owned = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(owned.Id, role: "Owner");
        await testData.AddMemberAsync(owned.Id);
        var pending = await testData.AddInvitationAsync(owned.Id);
        await testData.AddInvitationAsync(owned.Id, status: "Accepted", acceptedBy: owner);
        var cancelled = await testData.AddInvitationAsync(owned.Id, status: "Cancelled");
        await testData.SummarizeAsync(owned.Id);
        var invited = await testData.AddTenantAsync(status: "Suspended", suspensionReason: "Unpaid invoices");
        await testData.AddInvitationAsync(invited.Id, role: "Owner", language: "tr");
        await SummarizeOwnerInvitationAsync(invited.Id, name);
        var closed = await testData.AddTenantAsync(status: "Closed", suspensionReason: "Unpaid invoices", closedAt: DateTimeOffset.UtcNow);
        var purged = await testData.AddTenantAsync(status: "Purged");
        var suspended = await testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", timeZone: "Europe/Dublin");
        await testData.SuspendUserAsync(suspended);
        var deleted = await testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Ada", lastName: "Lovelace", timeZone: "Europe/Dublin");
        await testData.DeleteUserAsync(deleted);
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            UPDATE control_plane.tenants SET purged_at = now() WHERE id = '{purged.Id}';
            INSERT INTO control_plane.tenant_purge_sagas (id, state, waiting, tenant_id, version) VALUES ('{purged.Id}', 'Completed', ARRAY[]::text[], '{purged.Id}', 3);
            """,
            name);
        var before = await ControlPlaneRowsAsync(name);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, name));

        exitCode.ShouldBe(0, output);
        (await ControlPlaneRowsAsync(name)).ShouldBe(before);
        (await RefusingChecksAsync(
            name,
            $"UPDATE control_plane.tenants SET status = 'Cancelled' WHERE id = '{owned.Id}'",
            $"UPDATE control_plane.tenants SET closed_at = now() WHERE id = '{purged.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_count = member_count + 1 WHERE tenant_id = '{owned.Id}'",
            $"UPDATE control_plane.tenant_summaries SET member_count = -1, owner_count = -1 WHERE tenant_id = '{closed.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_status = 'Expired' WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_language = 'de' WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_expires_at = NULL WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_email = NULL WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.tenant_summaries SET member_count = 1, owner_count = 1 WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.users SET language = 'de' WHERE external_id = '{owner}'",
            $"UPDATE control_plane.users SET membership_count = -1 WHERE external_id = '{suspended}'",
            $"UPDATE control_plane.users SET suspended_by = NULL WHERE external_id = '{suspended}'",
            $"UPDATE control_plane.users SET first_name = 'Ada' WHERE external_id = '{deleted}'",
            $"UPDATE control_plane.invitations SET status = 'Expired' WHERE id = '{cancelled}'",
            $"UPDATE control_plane.invitations SET language = 'de' WHERE id = '{pending}'",
            $"UPDATE control_plane.invitations SET accepted_at = now() WHERE id = '{pending}'",
            $"UPDATE control_plane.tenant_purge_sagas SET state = 'Failed' WHERE id = '{purged.Id}'")).ShouldBe(
        [
            "ck_tenants_status",
            "ck_tenants_purged_is_a_tombstone",
            "ck_tenant_summaries_counts",
            "ck_tenant_summaries_counts",
            "ck_tenant_summaries_owner_invitation_status",
            "ck_tenant_summaries_owner_invitation_language",
            "ck_tenant_summaries_owner_invitation_together",
            "ck_tenant_summaries_pending_owner_invitation_has_email",
            "ck_tenant_summaries_owner_invitation_without_owner",
            "ck_users_language",
            "ck_users_membership_count",
            "ck_users_suspension",
            "ck_users_deleted_keeps_no_personal_data",
            "ck_invitations_status",
            "ck_invitations_language",
            "ck_invitations_acceptance",
            "ck_tenant_purge_sagas_state",
        ]);
    }

    // R15: the migration that adds Audit's CHECKs keeps the entries of both logs, and from then on the database refuses an entry whose
    // actor, kind or target is no value the code writes.
    [Fact]
    public async Task Migrate_AuditEntriesOfBothLogs_KeepsThemAndRefusesEachEntryThatBreaksACheck()
    {
        var name = await database.CreateDatabaseBeforeAuditMigrationAsync("AddCheckConstraints");
        var (tenantEntry, platformEntry) = await AddAuditEntriesAsync(name);
        var before = await AuditRowsAsync(name);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, name));

        exitCode.ShouldBe(0, output);
        (await AuditRowsAsync(name)).ShouldBe(before);
        (await RefusingChecksAsync(
            name,
            $"UPDATE audit.entries SET actor_kind = 'System' WHERE id = '{tenantEntry}'",
            $"UPDATE audit.entries SET kind = 'Query' WHERE id = '{tenantEntry}'",
            $"UPDATE audit.entries SET target_kind = 'Member' WHERE id = '{tenantEntry}'",
            $"UPDATE audit.platform_entries SET actor_kind = 'System' WHERE id = '{platformEntry}'",
            $"UPDATE audit.platform_entries SET kind = 'Query' WHERE id = '{platformEntry}'",
            $"UPDATE audit.platform_entries SET target_kind = 'Member' WHERE id = '{platformEntry}'")).ShouldBe(
        [
            "ck_entries_actor_kind",
            "ck_entries_kind",
            "ck_entries_target_kind",
            "ck_platform_entries_actor_kind",
            "ck_platform_entries_kind",
            "ck_platform_entries_target_kind",
        ]);
    }

    // R16: every foreign key restricts the deletion of a row in use, so each deletion is a step of the code, which the counts and the audit
    // follow. Two keys keep their rule: a custom role's permissions go with the role, and a finished invitation loses its deleted role.
    // The rows stay through the migration; a tenant or a user in use can no longer be deleted.
    [Fact]
    public async Task Migrate_RowsWithTheirParents_KeepsThemAndRestrictsEveryDeletionButTheTwoKept()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("MakeEachDeletionAStepOfTheCode");
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var member = await testData.AddMemberAsync(tenant.Id);
        await testData.AddInvitationAsync(tenant.Id);
        var role = await testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.profile.read");
        var admin = await testData.AddSystemAdminAsync();
        var before = await ControlPlaneRowsAsync(name);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, name));

        exitCode.ShouldBe(0, output);
        (await ControlPlaneRowsAsync(name)).ShouldBe(before);
        (await DeleteRulesAsync(name)).ShouldBe(
            """
            fk_invitations_roles_tenant_id_role_id: SET NULL (role_id)
            fk_invitations_tenants_tenant_id: RESTRICT
            fk_invitations_users_accepted_by: RESTRICT
            fk_invitations_users_invited_by: RESTRICT
            fk_memberships_roles_tenant_id_role_id: RESTRICT
            fk_memberships_tenants_tenant_id: RESTRICT
            fk_memberships_users_user_id: RESTRICT
            fk_role_permissions_roles_tenant_id_role_id: CASCADE
            fk_roles_tenants_tenant_id: RESTRICT
            fk_system_admins_users_user_id: RESTRICT
            fk_tenant_summaries_tenants_tenant_id: RESTRICT
            fk_users_users_suspended_by: RESTRICT
            """);
        (await RefusingKeysAsync(
            name,
            $"DELETE FROM control_plane.tenants WHERE id = '{tenant.Id}'",
            $"DELETE FROM control_plane.users WHERE external_id = '{member}'",
            $"DELETE FROM control_plane.users WHERE external_id = '{admin}'",
            $"DELETE FROM control_plane.roles WHERE id = '{role}'")).ShouldBe(
            ["fk_tenant_summaries_tenants_tenant_id", "fk_memberships_users_user_id", "fk_system_admins_users_user_id", "deleted"]);
    }

    // R17: a user's address and names have the lengths of the domain, as an invitation's address has. A user at each limit stays through
    // the migration; one character more is refused.
    [Fact]
    public async Task Migrate_AUserAtEachLimit_KeepsThemAndRefusesOneCharacterMore()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync("LimitTheLengthsOfAUsersAddressAndNames");
        var testData = new TestData(database, name);
        var user = await testData.AddUserAsync(email: $"{new string('a', 308)}@example.com", firstName: new string('F', 100), lastName: new string('L', 100));
        var before = await ControlPlaneRowsAsync(name);

        var (exitCode, output) = await MigrateCommand.RunAsync(database.ConnectionStringFor(DatabaseRoles.Owner, name));

        exitCode.ShouldBe(0, output);
        (await ControlPlaneRowsAsync(name)).ShouldBe(before);
        (await RefusedForLengthAsync(
            name,
            $"UPDATE control_plane.users SET email = 'a' || email WHERE external_id = '{user}'",
            $"UPDATE control_plane.users SET first_name = first_name || 'F' WHERE external_id = '{user}'",
            $"UPDATE control_plane.users SET last_name = last_name || 'L' WHERE external_id = '{user}'")).ShouldBe(["refused", "refused", "refused"]);
        (await ColumnTypesAsync(name, "users", "email", "first_name", "last_name")).ShouldBe(
            "email character varying(320), first_name character varying(100), last_name character varying(100)");
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

    // The tenant's summary keeps its latest Owner invitation while it has no Owner (R12), as a command writes it.
    private async Task SummarizeOwnerInvitationAsync(Guid tenantId, string databaseName) =>
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            UPDATE control_plane.tenant_summaries AS summary SET owner_invitation_email = invitation.email, owner_invitation_expires_at = invitation.expires_at,
                owner_invitation_status = invitation.status, owner_invitation_language = invitation.language
            FROM control_plane.invitations AS invitation WHERE summary.tenant_id = '{tenantId}' AND invitation.tenant_id = '{tenantId}'
            """,
            databaseName);

    // An entry of a tenant's log and one of the platform's, as Audit writes them.
    private async Task<(Guid TenantEntry, Guid PlatformEntry)> AddAuditEntriesAsync(string databaseName)
    {
        var (tenantEntry, platformEntry, tenantId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, kind, operation, target_kind, target_id, target_name)
            VALUES ('{tenantEntry}', '{tenantId}', now(), 'Platform', 'Command', 'tenant.closed', 'Tenant', '{tenantId}', 'Acme Ltd');
            INSERT INTO audit.platform_entries (id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id, target_name)
            VALUES ('{platformEntry}', now(), 'User', gen_random_uuid(), 'Command', 'tenant.closed', 'Tenant', '{tenantId}', 'Acme Ltd');
            """,
            databaseName);

        return (tenantEntry, platformEntry);
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

    // The CHECK that refuses each statement, or "stored" for a statement the database takes. Each runs in a transaction it rolls back, so
    // the rows stay as they were for the next.
    private async Task<List<string>> RefusingChecksAsync(string databaseName, params string[] statements)
    {
        var refusals = new List<string>();
        foreach (var statement in statements)
        {
            try
            {
                await database.ScalarAsSuperuserAsync<object>($"BEGIN; {statement}; ROLLBACK;", databaseName);
                refusals.Add("stored");
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.CheckViolation)
            {
                refusals.Add(exception.ConstraintName!);
            }
        }

        return refusals;
    }

    // Each foreign key of ControlPlane with what a deletion of its parent row does, one key on each line.
    private Task<string?> DeleteRulesAsync(string databaseName) =>
        database.ScalarAsSuperuserAsync<string>(
            """
            SELECT string_agg(conname || ': ' || substring(pg_get_constraintdef(oid) FROM 'ON DELETE (.*)$'), E'\n' ORDER BY conname)
            FROM (SELECT conname, oid FROM pg_constraint WHERE contype = 'f' AND connamespace = 'control_plane'::regnamespace) AS keys
            """,
            databaseName);

    // The foreign key that refuses each statement, or "deleted" for a statement the database takes. Each runs in a transaction it rolls
    // back, so the rows stay as they were for the next. PostgreSQL checks the keys of a parent in the order of their names.
    private async Task<List<string>> RefusingKeysAsync(string databaseName, params string[] statements)
    {
        var refusals = new List<string>();
        foreach (var statement in statements)
        {
            try
            {
                await database.ScalarAsSuperuserAsync<object>($"BEGIN; {statement}; ROLLBACK;", databaseName);
                refusals.Add("deleted");
            }
            catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.RestrictViolation or PostgresErrorCodes.ForeignKeyViolation)
            {
                refusals.Add(exception.ConstraintName!);
            }
        }

        return refusals;
    }

    // "refused" for each statement that the column's length refuses, "stored" for one the database takes; each is rolled back.
    private async Task<List<string>> RefusedForLengthAsync(string databaseName, params string[] statements)
    {
        var refusals = new List<string>();
        foreach (var statement in statements)
        {
            try
            {
                await database.ScalarAsSuperuserAsync<object>($"BEGIN; {statement}; ROLLBACK;", databaseName);
                refusals.Add("stored");
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.StringDataRightTruncation)
            {
                refusals.Add("refused");
            }
        }

        return refusals;
    }

    // The columns of the ControlPlane table with their types, in the order named.
    private Task<string?> ColumnTypesAsync(string databaseName, string table, params string[] columns) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(column_name || ' ' || data_type || coalesce('(' || character_maximum_length || ')', ''), ', '
                ORDER BY array_position(ARRAY[{string.Join(", ", columns.Select(column => $"'{column}'"))}]::text[], column_name::text))
            FROM information_schema.columns
            WHERE table_schema = 'control_plane' AND table_name = '{table}' AND column_name IN ({string.Join(", ", columns.Select(column => $"'{column}'"))})
            """,
            databaseName);

    // The roles of the tenant, by their kind and their name.
    private Task<string?> RolesOfAsync(Guid tenantId, string databaseName) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(format('%s|%s', built_in, name), ', ' ORDER BY built_in) FROM control_plane.roles WHERE tenant_id = '{tenantId}'",
            databaseName);

    // The kinds of the roles that the table's rows of the tenant name, joined only to a role of the same tenant.
    private Task<string?> RolesOfOwnTenantAsync(string table, Guid tenantId, string databaseName) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(roles.built_in, ', ' ORDER BY roles.built_in) FROM control_plane.{table}
            JOIN control_plane.roles ON roles.tenant_id = {table}.tenant_id AND roles.id = {table}.role_id
            WHERE {table}.tenant_id = '{tenantId}'
            """,
            databaseName);

    private Task<string?> OwnerOfAsync(string schema, string table, string databaseName) =>
        database.ScalarAsync<string>(
            $"SELECT tableowner FROM pg_tables WHERE schemaname = '{schema}' AND tablename = '{table}'", database: databaseName);
}
