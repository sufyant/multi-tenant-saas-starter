using Npgsql;

namespace Api.IntegrationTests.Persistence;

// The schema that the migration step creates on an empty database. First, the parts that the migrations write in SQL. No model snapshot
// holds them, so PendingModelChangesTests cannot see one go missing: forced row level security, each policy with its rule, each privilege
// of the application role, and the key and the index that EF Core cannot model. A new migration, or a squash of the migrations, leaves
// each of them as it is unless it means to change it. The purge function's owner, SECURITY DEFINER, search path and privileges are proven
// in PurgeTenantDataTests (R14). Then the rules of the model that the database keeps: each CHECK, each foreign key's delete rule and each
// length refuses the row that breaks it (R15, R16, R17). A database of its own holds only what the migration step creates, without the
// probe tables of the main database, and the rows of one test.
public sealed class MigratedSchemaTests(Database database)
{
    // R1, R11: each table under row level security, and whether the policies bind its owner too.
    [Fact]
    public async Task Migrate_EmptyDatabase_ForcesRowLevelSecurityOnEveryTableThatHasIt()
    {
        var migrated = await database.CreateMigratedDatabaseAsync();

        var tables = await database.ScalarAsSuperuserAsync<string>(
            """
            SELECT string_agg(format('%s.%s: %s', n.nspname, c.relname, CASE WHEN c.relforcerowsecurity THEN 'forced' ELSE 'not forced' END),
                E'\n' ORDER BY n.nspname, c.relname)
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relrowsecurity
            """,
            migrated);

        tables.ShouldBe(
            """
            audit.entries: forced
            audit.platform_entries: forced
            control_plane.invitations: forced
            control_plane.memberships: forced
            control_plane.role_permissions: forced
            control_plane.roles: forced
            control_plane.tenant_purge_sagas: forced
            """);
    }

    // R1, R11: each policy with its command, its roles, and the rows it lets a transaction read and write.
    [Fact]
    public async Task Migrate_EmptyDatabase_WritesEveryPolicyWithItsRule()
    {
        var migrated = await database.CreateMigratedDatabaseAsync();

        var policies = await database.ScalarAsSuperuserAsync<string>(
            """
            SELECT string_agg(format('%s.%s %s: %s %s %s USING %s WITH CHECK %s', schemaname, tablename, policyname, permissive, cmd, roles,
                coalesce(qual, '-'), coalesce(with_check, '-')), E'\n' ORDER BY schemaname, tablename, policyname)
            FROM pg_policies
            """,
            migrated);

        policies.ShouldBe(
            """
            audit.entries tenant_isolation: PERMISSIVE ALL {public} USING (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid) WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)
            audit.platform_entries platform_insert: PERMISSIVE INSERT {public} USING - WITH CHECK true
            audit.platform_entries platform_read: PERMISSIVE SELECT {public} USING ((NULLIF(current_setting('app.tenant_id'::text, true), ''::text) IS NULL) AND (NULLIF(current_setting('app.user_id'::text, true), ''::text) IS NULL)) WITH CHECK -
            control_plane.invitations own_accepted_invitations: PERMISSIVE SELECT {public} USING (accepted_by = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid) WITH CHECK -
            control_plane.invitations tenant_isolation: PERMISSIVE ALL {public} USING (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid) WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)
            control_plane.memberships own_memberships: PERMISSIVE SELECT {public} USING (user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid) WITH CHECK -
            control_plane.memberships tenant_isolation: PERMISSIVE ALL {public} USING (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid) WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)
            control_plane.role_permissions tenant_isolation: PERMISSIVE ALL {public} USING (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid) WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)
            control_plane.roles own_roles: PERMISSIVE SELECT {public} USING (EXISTS ( SELECT
               FROM control_plane.memberships
              WHERE ((memberships.tenant_id = roles.tenant_id) AND (memberships.role_id = roles.id) AND (memberships.user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)))) WITH CHECK -
            control_plane.roles tenant_isolation: PERMISSIVE ALL {public} USING (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid) WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)
            control_plane.tenant_purge_sagas tenant_isolation: PERMISSIVE ALL {public} USING (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid) WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)
            """);
    }

    // R2, R7, R14, K3: every privilege that the application role holds, on schemas, tables, functions and the tables to come. It holds
    // SELECT and INSERT only on both audit logs, and may only execute the purge function there. The message storage grants the role every
    // table and sequence of its schema, whichever tables a version of Wolverine has, so its tables are not listed one by one; a host on a
    // migrated database proves that the role uses them (StartApplication_OnAMigratedDatabase_IsReady).
    [Fact]
    public async Task Migrate_EmptyDatabase_GrantsTheApplicationRoleOnlyItsPrivileges()
    {
        var migrated = await database.CreateMigratedDatabaseAsync();

        var privileges = await database.ScalarAsSuperuserAsync<string>(
            """
            WITH granted (object, privilege) AS (
                SELECT 'schema ' || n.nspname, acl.privilege_type
                FROM pg_namespace n, aclexplode(n.nspacl) acl
                WHERE acl.grantee = 'api_application'::regrole
                UNION ALL
                SELECT CASE c.relkind WHEN 'S' THEN 'sequence ' ELSE 'table ' END || n.nspname || '.' || c.relname, acl.privilege_type
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace, aclexplode(c.relacl) acl
                WHERE acl.grantee = 'api_application'::regrole AND n.nspname <> 'wolverine'
                UNION ALL
                SELECT 'function ' || p.oid::regprocedure, acl.privilege_type
                FROM pg_proc p, aclexplode(p.proacl) acl
                WHERE acl.grantee = 'api_application'::regrole
                UNION ALL
                SELECT 'default for ' || CASE d.defaclobjtype WHEN 'r' THEN 'tables' ELSE d.defaclobjtype::text END || ' in ' || d.defaclnamespace::regnamespace,
                    acl.privilege_type
                FROM pg_default_acl d, aclexplode(d.defaclacl) acl
                WHERE acl.grantee = 'api_application'::regrole)
            SELECT string_agg(object || ': ' || privileges, E'\n' ORDER BY object)
            FROM (SELECT object, string_agg(privilege, ', ' ORDER BY privilege) AS privileges FROM granted GROUP BY object) AS objects
            """,
            migrated);

        privileges.ShouldBe(
            """
            default for tables in api: DELETE, INSERT, SELECT, UPDATE
            default for tables in audit: INSERT, SELECT
            default for tables in control_plane: DELETE, INSERT, SELECT, UPDATE
            function audit.purge_tenant_entries(): EXECUTE
            schema api: USAGE
            schema audit: USAGE
            schema control_plane: USAGE
            schema wolverine: USAGE
            table api.__ef_migrations_history: SELECT
            table api.idempotency_keys: DELETE, INSERT, SELECT, UPDATE
            table audit.__ef_migrations_history: SELECT
            table audit.entries: INSERT, SELECT
            table audit.platform_entries: INSERT, SELECT
            table control_plane.__ef_migrations_history: SELECT
            table control_plane.default_plans: DELETE, INSERT, SELECT, UPDATE
            table control_plane.entitlement_overrides: DELETE, INSERT, SELECT, UPDATE
            table control_plane.invitations: DELETE, INSERT, SELECT, UPDATE
            table control_plane.memberships: DELETE, INSERT, SELECT, UPDATE
            table control_plane.plan_entitlements: DELETE, INSERT, SELECT, UPDATE
            table control_plane.plans: DELETE, INSERT, SELECT, UPDATE
            table control_plane.role_permissions: DELETE, INSERT, SELECT, UPDATE
            table control_plane.roles: DELETE, INSERT, SELECT, UPDATE
            table control_plane.system_admins: DELETE, INSERT, SELECT, UPDATE
            table control_plane.tenant_purge_sagas: DELETE, INSERT, SELECT, UPDATE
            table control_plane.tenant_summaries: DELETE, INSERT, SELECT, UPDATE
            table control_plane.tenants: DELETE, INSERT, SELECT, UPDATE
            table control_plane.users: DELETE, INSERT, SELECT, UPDATE
            """);
    }

    // A3: the key that keeps a finished invitation when its custom role is deleted, and the index that keeps a role's name unique in its
    // tenant without regard to case. EF Core can model neither, so a migration writes both in SQL.
    [Fact]
    public async Task Migrate_EmptyDatabase_WritesTheKeyAndTheIndexThatTheModelCannotHold()
    {
        var migrated = await database.CreateMigratedDatabaseAsync();

        var written = await database.ScalarAsSuperuserAsync<string>(
            """
            SELECT concat_ws(E'\n',
                (SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'fk_invitations_roles_tenant_id_role_id'),
                pg_get_indexdef('control_plane.ix_roles_tenant_id_lower_name'::regclass))
            """,
            migrated);

        written.ShouldBe(
            """
            FOREIGN KEY (tenant_id, role_id) REFERENCES control_plane.roles(tenant_id, id) ON DELETE SET NULL (role_id)
            CREATE UNIQUE INDEX ix_roles_tenant_id_lower_name ON control_plane.roles USING btree (tenant_id, lower((name)::text))
            """);
    }

    // R15: a single-row rule of the code is a CHECK. Rows of every kind, as the code writes them, are stored: tenants of each kind with
    // their summaries, one with an Owner invitation, users suspended and deleted, invitations of each status, a purge's record. Each
    // statement then breaks one rule, and the database refuses it and names the rule.
    [Fact]
    public async Task WriteRow_BreakingAControlPlaneCheck_IsRefusedByThatCheck()
    {
        var migrated = await database.CreateMigratedDatabaseAsync();
        var testData = new TestData(database, migrated);
        var owned = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(owned.Id, role: "Owner");
        await testData.AddMemberAsync(owned.Id);
        var pending = await testData.AddInvitationAsync(owned.Id);
        await testData.AddInvitationAsync(owned.Id, status: "Accepted", acceptedBy: owner);
        var cancelled = await testData.AddInvitationAsync(owned.Id, status: "Cancelled");
        await testData.SummarizeAsync(owned.Id);
        var invited = await testData.AddTenantAsync(status: "Suspended", suspensionReason: "Unpaid invoices");
        await testData.AddInvitationAsync(invited.Id, role: "Owner", language: "tr");
        await SummarizeOwnerInvitationAsync(invited.Id, migrated);
        var closed = await testData.AddTenantAsync(status: "Closed", suspensionReason: "Unpaid invoices", closedAt: DateTimeOffset.UtcNow);
        var purged = await testData.AddTenantAsync(status: "Purged");
        var suspended = await testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", timeZone: "Europe/Dublin");
        await testData.SuspendUserAsync(suspended);
        var deleted = await testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Ada", lastName: "Lovelace", timeZone: "Europe/Dublin");
        await testData.DeleteUserAsync(deleted);
        await database.ScalarAsSuperuserAsync<object>(
            $"INSERT INTO control_plane.tenant_purge_sagas (id, state, waiting, tenant_id, version) VALUES ('{purged.Id}', 'Completed', ARRAY[]::text[], '{purged.Id}', 3)",
            migrated);

        var refusals = await RefusingAsync(
            migrated,
            PostgresErrorCodes.CheckViolation,
            $"UPDATE control_plane.tenants SET status = 'Cancelled' WHERE id = '{owned.Id}'",
            $"UPDATE control_plane.tenants SET closed_at = now() WHERE id = '{purged.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_count = member_count + 1 WHERE tenant_id = '{owned.Id}'",
            $"UPDATE control_plane.tenant_summaries SET member_count = -1, owner_count = -1 WHERE tenant_id = '{closed.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_status = 'Expired' WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_language = 'de' WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_expires_at = NULL WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_language = NULL WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.tenant_summaries SET owner_invitation_email = NULL WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.tenant_summaries SET member_count = 1, owner_count = 1 WHERE tenant_id = '{invited.Id}'",
            $"UPDATE control_plane.users SET language = 'de' WHERE external_id = '{owner}'",
            $"UPDATE control_plane.users SET membership_count = -1 WHERE external_id = '{suspended}'",
            $"UPDATE control_plane.users SET suspended_by = NULL WHERE external_id = '{suspended}'",
            $"UPDATE control_plane.users SET first_name = 'Ada' WHERE external_id = '{deleted}'",
            $"UPDATE control_plane.invitations SET status = 'Expired' WHERE id = '{cancelled}'",
            $"UPDATE control_plane.invitations SET language = 'de' WHERE id = '{pending}'",
            $"UPDATE control_plane.invitations SET accepted_at = now() WHERE id = '{pending}'",
            $"UPDATE control_plane.tenant_purge_sagas SET state = 'Failed' WHERE id = '{purged.Id}'");

        refusals.ShouldBe(
        [
            "ck_tenants_status",
            "ck_tenants_purged_is_a_tombstone",
            "ck_tenant_summaries_counts",
            "ck_tenant_summaries_counts",
            "ck_tenant_summaries_owner_invitation_status",
            "ck_tenant_summaries_owner_invitation_language",
            "ck_tenant_summaries_owner_invitation_together",
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

    // R15: the same for Audit. An entry of each log is stored, and the database refuses an entry whose actor, kind or target is no value
    // the code writes.
    [Fact]
    public async Task WriteEntry_BreakingAnAuditCheck_IsRefusedByThatCheck()
    {
        var migrated = await database.CreateMigratedDatabaseAsync();
        var (tenantEntry, platformEntry, tenantId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, kind, operation, target_kind, target_id, target_name)
            VALUES ('{tenantEntry}', '{tenantId}', now(), 'Platform', 'Command', 'tenant.closed', 'Tenant', '{tenantId}', 'Acme Ltd');
            INSERT INTO audit.platform_entries (id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id, target_name)
            VALUES ('{platformEntry}', now(), 'User', gen_random_uuid(), 'Command', 'tenant.closed', 'Tenant', '{tenantId}', 'Acme Ltd');
            """,
            migrated);

        var refusals = await RefusingAsync(
            migrated,
            PostgresErrorCodes.CheckViolation,
            $"UPDATE audit.entries SET actor_kind = 'System' WHERE id = '{tenantEntry}'",
            $"UPDATE audit.entries SET kind = 'Query' WHERE id = '{tenantEntry}'",
            $"UPDATE audit.entries SET target_kind = 'Member' WHERE id = '{tenantEntry}'",
            $"UPDATE audit.platform_entries SET actor_kind = 'System' WHERE id = '{platformEntry}'",
            $"UPDATE audit.platform_entries SET kind = 'Query' WHERE id = '{platformEntry}'",
            $"UPDATE audit.platform_entries SET target_kind = 'Member' WHERE id = '{platformEntry}'");

        refusals.ShouldBe(
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
    // follow. Two keys do not: a custom role's permissions go with the role, and a finished invitation loses its deleted role.
    [Fact]
    public async Task DeleteRow_ThatAnotherRowNames_IsRestrictedButForTheTwoKeysKept()
    {
        var migrated = await database.CreateMigratedDatabaseAsync();
        var testData = new TestData(database, migrated);
        var tenant = await testData.AddTenantAsync();
        var member = await testData.AddMemberAsync(tenant.Id);
        await testData.AddInvitationAsync(tenant.Id);
        var role = await testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.profile.read");
        var admin = await testData.AddSystemAdminAsync();

        var rules = await database.ScalarAsSuperuserAsync<string>(
            """
            SELECT string_agg(conname || ': ' || substring(pg_get_constraintdef(oid) FROM 'ON DELETE (.*)$'), E'\n' ORDER BY conname)
            FROM (SELECT conname, oid FROM pg_constraint WHERE contype = 'f' AND connamespace = 'control_plane'::regnamespace) AS keys
            """,
            migrated);
        var refusals = await RefusingAsync(
            migrated,
            PostgresErrorCodes.ForeignKeyViolation,
            $"DELETE FROM control_plane.tenants WHERE id = '{tenant.Id}'",
            $"DELETE FROM control_plane.users WHERE external_id = '{member}'",
            $"DELETE FROM control_plane.users WHERE external_id = '{admin}'",
            $"DELETE FROM control_plane.roles WHERE id = '{role}'");

        rules.ShouldBe(
            """
            fk_default_plans_plans_plan_id: RESTRICT
            fk_entitlement_overrides_tenants_tenant_id: RESTRICT
            fk_entitlement_overrides_users_set_by: RESTRICT
            fk_invitations_roles_tenant_id_role_id: SET NULL (role_id)
            fk_invitations_tenants_tenant_id: RESTRICT
            fk_invitations_users_accepted_by: RESTRICT
            fk_invitations_users_invited_by: RESTRICT
            fk_memberships_roles_tenant_id_role_id: RESTRICT
            fk_memberships_tenants_tenant_id: RESTRICT
            fk_memberships_users_user_id: RESTRICT
            fk_plan_entitlements_plans_plan_id: RESTRICT
            fk_role_permissions_roles_tenant_id_role_id: CASCADE
            fk_roles_tenants_tenant_id: RESTRICT
            fk_system_admins_users_user_id: RESTRICT
            fk_tenant_summaries_tenants_tenant_id: RESTRICT
            fk_tenants_plans_plan_id: RESTRICT
            fk_users_users_suspended_by: RESTRICT
            """);
        refusals.ShouldBe(["fk_roles_tenants_tenant_id", "fk_memberships_users_user_id", "fk_system_admins_users_user_id", "taken"]);
    }

    // R17: a user's address and names have the lengths of the domain, as an invitation's address has. A user at each limit is stored; one
    // character more is refused.
    [Fact]
    public async Task WriteUser_OneCharacterOverALimit_IsRefused()
    {
        var migrated = await database.CreateMigratedDatabaseAsync();
        var testData = new TestData(database, migrated);
        var user = await testData.AddUserAsync(email: $"{new string('a', 308)}@example.com", firstName: new string('F', 100), lastName: new string('L', 100));

        var refusals = await RefusingAsync(
            migrated,
            PostgresErrorCodes.StringDataRightTruncation,
            $"UPDATE control_plane.users SET email = 'a' || email WHERE external_id = '{user}'",
            $"UPDATE control_plane.users SET first_name = first_name || 'F' WHERE external_id = '{user}'",
            $"UPDATE control_plane.users SET last_name = last_name || 'L' WHERE external_id = '{user}'");

        refusals.ShouldBe(["refused", "refused", "refused"]);
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

    // What refuses each statement: the name of the constraint, or "refused" for a refusal that names none, such as a length; "taken" for
    // a statement the database takes. Each runs in a transaction it rolls back, so the rows stay as they were for the next. PostgreSQL
    // checks the keys of a parent in the order of their triggers' names, which follow the order in which the migration created the keys,
    // and names the first key that refuses. A key that restricts a deletion refuses it with RestrictViolation.
    private async Task<List<string>> RefusingAsync(string databaseName, string sqlState, params string[] statements)
    {
        var refusals = new List<string>();
        foreach (var statement in statements)
        {
            try
            {
                await database.ScalarAsSuperuserAsync<object>($"BEGIN; {statement}; ROLLBACK;", databaseName);
                refusals.Add("taken");
            }
            catch (PostgresException exception) when (exception.SqlState == sqlState || exception.SqlState == PostgresErrorCodes.RestrictViolation)
            {
                refusals.Add(exception.ConstraintName ?? "refused");
            }
        }

        return refusals;
    }
}
