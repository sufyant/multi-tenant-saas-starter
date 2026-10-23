using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            // Privileges come from migrations. Audit entries are never changed once written, so the application role
            // may only add and read them.
            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA audit TO api_application;
                ALTER DEFAULT PRIVILEGES IN SCHEMA audit GRANT SELECT, INSERT ON TABLES TO api_application;
                """);

            migrationBuilder.CreateTable(
                name: "entries",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    operation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entries", x => new { x.occurred_at, x.id });
                    table.CheckConstraint("ck_entries_actor_kind", "actor_kind IN ('User', 'Platform')");
                    table.CheckConstraint("ck_entries_kind", "kind IN ('Command')");
                    table.CheckConstraint("ck_entries_target_kind", "target_kind IN ('Tenant', 'User', 'Invitation', 'Role')");
                });

            migrationBuilder.CreateTable(
                name: "platform_entries",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    operation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    target_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_entries", x => new { x.occurred_at, x.id });
                    table.CheckConstraint("ck_platform_entries_actor_kind", "actor_kind IN ('User', 'Platform')");
                    table.CheckConstraint("ck_platform_entries_kind", "kind IN ('Command')");
                    table.CheckConstraint("ck_platform_entries_target_kind", "target_kind IN ('Tenant', 'User', 'Invitation', 'Role')");
                });

            // K3, R6, R11: the platform's log has no tenant column. The application role gets SELECT and INSERT only, from the
            // schema's default privileges. Row level security, forced, keeps it from a transaction that declares a tenant or a user:
            // such a transaction reads no row, and Audit, in a tenant's transaction, may still add the platform's record.
            migrationBuilder.Sql("""
                ALTER TABLE audit.platform_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE audit.platform_entries FORCE ROW LEVEL SECURITY;
                CREATE POLICY platform_read ON audit.platform_entries FOR SELECT
                    USING (NULLIF(current_setting('app.tenant_id', true), '') IS NULL AND NULLIF(current_setting('app.user_id', true), '') IS NULL);
                CREATE POLICY platform_insert ON audit.platform_entries FOR INSERT WITH CHECK (true);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_entries_tenant_id_occurred_at_id",
                schema: "audit",
                table: "entries",
                columns: new[] { "tenant_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_platform_entries_target_kind_target_id_occurred_at_id",
                schema: "audit",
                table: "platform_entries",
                columns: new[] { "target_kind", "target_id", "occurred_at", "id" });

            // R14, K3: the application cannot change an audit entry or delete one alone; it holds SELECT and INSERT only. The tenant purge
            // deletes a tenant's whole log through this function, which runs as its owner, the migration role, and deletes only the entries
            // of the tenant the transaction declares (R4). Row level security, forced, still binds the owner, so the policy and the WHERE
            // clause both keep the deletion to that tenant. The search path is fixed, so no object of another schema stands in for a name
            // (PostgreSQL documentation, CREATE FUNCTION, "Writing SECURITY DEFINER Functions Safely"). Only the application may execute it.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit.purge_tenant_entries() RETURNS integer
                    LANGUAGE plpgsql
                    SECURITY DEFINER
                    SET search_path = pg_catalog, pg_temp
                AS $$
                DECLARE
                    tenant uuid := NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                    deleted integer;
                BEGIN
                    IF tenant IS NULL THEN
                        RAISE EXCEPTION 'audit.purge_tenant_entries() deletes the entries of the declared tenant, and no tenant is declared';
                    END IF;

                    DELETE FROM audit.entries WHERE tenant_id = tenant;
                    GET DIAGNOSTICS deleted = ROW_COUNT;
                    RETURN deleted;
                END
                $$;
                REVOKE ALL ON FUNCTION audit.purge_tenant_entries() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION audit.purge_tenant_entries() TO api_application;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION audit.purge_tenant_entries();");

            migrationBuilder.DropTable(
                name: "entries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "platform_entries",
                schema: "audit");

            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES IN SCHEMA audit REVOKE SELECT, INSERT ON TABLES FROM api_application;
                REVOKE USAGE ON SCHEMA audit FROM api_application;
                """);
        }
    }
}
