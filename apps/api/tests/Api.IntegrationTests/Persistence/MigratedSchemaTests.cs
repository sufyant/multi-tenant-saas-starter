namespace Api.IntegrationTests.Persistence;

// The parts of the schema that the migrations write in SQL. No model snapshot holds them, so PendingModelChangesTests cannot see one go
// missing: forced row level security, each policy with its rule, each privilege of the application role, and the key and the index that
// EF Core cannot model. A new migration, or a squash of the migrations, leaves each of them as it is unless it means to change it. The
// purge function's owner, SECURITY DEFINER, search path and privileges are proven in PurgeTenantDataTests (R14). A database of its own
// holds only what the migration step creates, without the probe tables of the main database.
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
            table control_plane.invitations: DELETE, INSERT, SELECT, UPDATE
            table control_plane.memberships: DELETE, INSERT, SELECT, UPDATE
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
}
