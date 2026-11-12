using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_retentions",
                schema: "audit",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    days = table.Column<int>(type: "integer", nullable: true),
                    held_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    held_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_retentions", x => x.tenant_id);
                    table.CheckConstraint("ck_tenant_retentions_days", "days IS NULL OR days >= 30");
                    table.CheckConstraint("ck_tenant_retentions_hold", "held_from IS NULL OR held_until IS NOT NULL");
                });

            // K14, K6o: the row is the tenant's state of the retention, which its writers change under its lock and the tenant purge deletes.
            // The schema's default privileges give SELECT and INSERT; the row also needs UPDATE and DELETE. The logs keep SELECT and INSERT
            // only (K3).
            migrationBuilder.Sql("GRANT UPDATE, DELETE ON audit.tenant_retentions TO api_application;");

            // R19, R11: the retention deletes through two functions of the migration role, in the shape of R14 and U8. Each refuses a moment
            // later than 30 days ago (AuditRetention.Floor), by the clock of the call, so a fault in the application cannot delete a recent
            // log. Row level security, forced, binds the owner: the tenant's policy keeps the tenant's deletion to the declared tenant, and on
            // the platform's log platform_read lets the owner read in a transaction that declares nobody, while owner_delete admits only the
            // entries older than 30 days. The policy and each function's WHERE clause both bound a deletion. The search path is fixed
            // (PostgreSQL documentation, CREATE FUNCTION, "Writing SECURITY DEFINER Functions Safely"). Only the application may execute them.
            migrationBuilder.Sql("""
                CREATE POLICY owner_delete ON audit.platform_entries FOR DELETE TO api_owner
                    USING (occurred_at < now() - interval '30 days');

                CREATE FUNCTION audit.delete_expired_entries(before timestamptz, batch integer) RETURNS integer
                    LANGUAGE plpgsql
                    SECURITY DEFINER
                    SET search_path = pg_catalog, pg_temp
                AS $$
                DECLARE
                    tenant uuid := NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                    deleted integer;
                BEGIN
                    IF tenant IS NULL THEN
                        RAISE EXCEPTION 'audit.delete_expired_entries() deletes the entries of the declared tenant, and no tenant is declared';
                    END IF;

                    IF before IS NULL OR before > clock_timestamp() - interval '30 days' OR batch IS NULL OR batch < 1 THEN
                        RAISE EXCEPTION 'audit.delete_expired_entries() deletes a positive batch of entries older than 30 days';
                    END IF;

                    DELETE FROM audit.entries
                    WHERE tenant_id = tenant AND (occurred_at, id) IN (
                        SELECT occurred_at, id FROM audit.entries WHERE tenant_id = tenant AND occurred_at < before ORDER BY occurred_at LIMIT batch);
                    GET DIAGNOSTICS deleted = ROW_COUNT;
                    RETURN deleted;
                END
                $$;
                REVOKE ALL ON FUNCTION audit.delete_expired_entries(timestamptz, integer) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION audit.delete_expired_entries(timestamptz, integer) TO api_application;

                CREATE FUNCTION audit.delete_expired_platform_entries(before timestamptz, batch integer) RETURNS integer
                    LANGUAGE plpgsql
                    SECURITY DEFINER
                    SET search_path = pg_catalog, pg_temp
                AS $$
                DECLARE
                    deleted integer;
                BEGIN
                    IF NULLIF(current_setting('app.tenant_id', true), '') IS NOT NULL OR NULLIF(current_setting('app.user_id', true), '') IS NOT NULL THEN
                        RAISE EXCEPTION 'audit.delete_expired_platform_entries() runs in a transaction that declares neither a tenant nor a user';
                    END IF;

                    IF before IS NULL OR before > clock_timestamp() - interval '30 days' OR batch IS NULL OR batch < 1 THEN
                        RAISE EXCEPTION 'audit.delete_expired_platform_entries() deletes a positive batch of entries older than 30 days';
                    END IF;

                    DELETE FROM audit.platform_entries
                    WHERE (occurred_at, id) IN (SELECT occurred_at, id FROM audit.platform_entries WHERE occurred_at < before ORDER BY occurred_at LIMIT batch);
                    GET DIAGNOSTICS deleted = ROW_COUNT;
                    RETURN deleted;
                END
                $$;
                REVOKE ALL ON FUNCTION audit.delete_expired_platform_entries(timestamptz, integer) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION audit.delete_expired_platform_entries(timestamptz, integer) TO api_application;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION audit.delete_expired_entries(timestamptz, integer);
                DROP FUNCTION audit.delete_expired_platform_entries(timestamptz, integer);
                DROP POLICY owner_delete ON audit.platform_entries;
                """);

            migrationBuilder.DropTable(
                name: "tenant_retentions",
                schema: "audit");
        }
    }
}
