using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 35: every notification type is about the person's account or access, so the catalog fixes its channels and nobody
            // chooses them (U4). Both preference tables go, with their rows, policies and CHECKs. The template is not in production, so
            // this is no expansion: the version before fails on them during the deploy (ARCHITECTURE.md, section 13). The model never
            // mapped email_preferences, so plain SQL drops it.
            migrationBuilder.DropTable(
                name: "notification_preferences",
                schema: "notifications");

            migrationBuilder.Sql("DROP TABLE notifications.email_preferences;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_preferences",
                schema: "notifications",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categories = table.Column<string>(type: "jsonb", nullable: false),
                    channels = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.user_id);
                    table.CheckConstraint("ck_notification_preferences_categories", "jsonb_typeof(categories) = 'object' AND (categories - ARRAY['access', 'member_invitations', 'tenant_plans', 'platform_tenants']::text[]) = '{}'::jsonb");
                    table.CheckConstraint("ck_notification_preferences_channels", "jsonb_typeof(channels) = 'object' AND (channels - ARRAY['Web', 'Email']::text[]) = '{}'::jsonb");
                    table.CheckConstraint("ck_notification_preferences_not_default", "NOT (channels = '{}'::jsonb AND categories = '{}'::jsonb)");
                });

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

            // The rows are gone; the tables come back empty, each with its own policy (R4, R11).
            migrationBuilder.Sql("""
                ALTER TABLE notifications.notification_preferences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE notifications.notification_preferences FORCE ROW LEVEL SECURITY;
                CREATE POLICY own_notification_preferences ON notifications.notification_preferences TO api_application
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                ALTER TABLE notifications.email_preferences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE notifications.email_preferences FORCE ROW LEVEL SECURITY;
                CREATE POLICY own_email_preferences ON notifications.email_preferences TO api_application
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);
        }
    }
}
