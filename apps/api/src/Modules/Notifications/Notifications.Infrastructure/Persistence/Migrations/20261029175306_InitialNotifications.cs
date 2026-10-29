using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            // Privileges come from migrations. Row level security keeps every privilege of the application to the declared user's rows.
            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA notifications TO api_application;
                ALTER DEFAULT PRIVILEGES IN SCHEMA notifications GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO api_application;
                """);

            migrationBuilder.CreateTable(
                name: "email_preferences",
                schema: "notifications",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    categories = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_preferences", x => x.user_id);
                    table.CheckConstraint("ck_email_preferences_categories", "jsonb_typeof(categories) = 'object' AND (categories - ARRAY['access', 'member_invitations', 'tenant_plans', 'platform_tenants']::text[]) = '{}'::jsonb");
                    table.CheckConstraint("ck_email_preferences_not_default", "NOT (enabled AND categories = '{}'::jsonb)");
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bell = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    values = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_bell", "bell IN ('Tenant', 'Personal', 'Platform')");
                    table.CheckConstraint("ck_notifications_tenant", "(bell <> 'Tenant' OR tenant_id IS NOT NULL) AND (bell <> 'Personal' OR tenant_id IS NULL)");
                    table.CheckConstraint("ck_notifications_type", "type IN ('invitation.accepted', 'member.removed', 'member.role_changed', 'tenant.owner_joined', 'tenant.plan_changed', 'tenant.purged', 'tenant.status_changed')");
                    table.CheckConstraint("ck_notifications_values", "jsonb_typeof(\"values\") = 'object'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_created_at",
                schema: "notifications",
                table: "notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id",
                schema: "notifications",
                table: "notifications",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_unread",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "user_id", "bell", "tenant_id" },
                filter: "read_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_created_at_id",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "user_id", "created_at", "id" });

            // R4, R11, U3: both tables belong to a user, not to a tenant (R6). Row level security, forced, shows the application only the
            // rows of the user the transaction declares, for every command: a transaction that declares a tenant, or nobody, sees none.
            migrationBuilder.Sql("""
                ALTER TABLE notifications.notifications ENABLE ROW LEVEL SECURITY;
                ALTER TABLE notifications.notifications FORCE ROW LEVEL SECURITY;
                CREATE POLICY own_notifications ON notifications.notifications TO api_application
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                ALTER TABLE notifications.email_preferences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE notifications.email_preferences FORCE ROW LEVEL SECURITY;
                CREATE POLICY own_email_preferences ON notifications.email_preferences TO api_application
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);

            // U8, in the shape of R14: the tenant purge and the retention delete across users, which no transaction of the application
            // can, because it declares one user at most. Each goes through one function that runs as its owner, the migration role, which
            // the application never connects as (R3, R7). Row level security, forced, binds the owner too, so two policies for the owner
            // alone let the functions read and delete; the application's own policy stays as it is. Each function deletes only its own
            // rows: the purge those about the tenant the transaction declares, and the retention a batch of those older than a moment that
            // is at least a day old. The search path is fixed (PostgreSQL documentation, CREATE FUNCTION, "Writing SECURITY DEFINER
            // Functions Safely"). Only the application may execute them.
            migrationBuilder.Sql("""
                CREATE POLICY owner_read ON notifications.notifications FOR SELECT TO api_owner USING (true);
                CREATE POLICY owner_delete ON notifications.notifications FOR DELETE TO api_owner USING (true);

                CREATE FUNCTION notifications.purge_tenant_notifications() RETURNS integer
                    LANGUAGE plpgsql
                    SECURITY DEFINER
                    SET search_path = pg_catalog, pg_temp
                AS $$
                DECLARE
                    tenant uuid := NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                    deleted integer;
                BEGIN
                    IF tenant IS NULL THEN
                        RAISE EXCEPTION 'notifications.purge_tenant_notifications() deletes the notifications about the declared tenant, and no tenant is declared';
                    END IF;

                    DELETE FROM notifications.notifications WHERE tenant_id = tenant;
                    GET DIAGNOSTICS deleted = ROW_COUNT;
                    RETURN deleted;
                END
                $$;
                REVOKE ALL ON FUNCTION notifications.purge_tenant_notifications() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION notifications.purge_tenant_notifications() TO api_application;

                CREATE FUNCTION notifications.delete_expired_notifications(before timestamptz, batch integer) RETURNS integer
                    LANGUAGE plpgsql
                    SECURITY DEFINER
                    SET search_path = pg_catalog, pg_temp
                AS $$
                DECLARE
                    deleted integer;
                BEGIN
                    IF before IS NULL OR before > now() - interval '1 day' OR batch IS NULL OR batch < 1 THEN
                        RAISE EXCEPTION 'notifications.delete_expired_notifications() deletes a positive batch of notifications older than a day';
                    END IF;

                    DELETE FROM notifications.notifications
                    WHERE id IN (SELECT id FROM notifications.notifications WHERE created_at < before ORDER BY created_at LIMIT batch);
                    GET DIAGNOSTICS deleted = ROW_COUNT;
                    RETURN deleted;
                END
                $$;
                REVOKE ALL ON FUNCTION notifications.delete_expired_notifications(timestamptz, integer) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION notifications.delete_expired_notifications(timestamptz, integer) TO api_application;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION notifications.purge_tenant_notifications();
                DROP FUNCTION notifications.delete_expired_notifications(timestamptz, integer);
                """);

            migrationBuilder.DropTable(
                name: "email_preferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "notifications");

            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES IN SCHEMA notifications REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM api_application;
                REVOKE USAGE ON SCHEMA notifications FROM api_application;
                """);
        }
    }
}
