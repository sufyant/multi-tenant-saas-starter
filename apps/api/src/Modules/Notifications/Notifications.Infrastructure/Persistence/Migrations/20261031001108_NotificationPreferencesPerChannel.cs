using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationPreferencesPerChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 33-backend-4: the preferences become a table of categories and channels (U5). This is an expansion (docs/deployment.md,
            // section 8): the version before still serves during the deploy and reads and writes email_preferences, so that table stays,
            // with its policy and its CHECKs, until a later release drops it (ARCHITECTURE.md, section 13). The model no longer maps it.
            migrationBuilder.CreateTable(
                name: "notification_preferences",
                schema: "notifications",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channels = table.Column<string>(type: "jsonb", nullable: false),
                    categories = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.user_id);
                    table.CheckConstraint("ck_notification_preferences_categories", "jsonb_typeof(categories) = 'object' AND (categories - ARRAY['access', 'member_invitations', 'tenant_plans', 'platform_tenants']::text[]) = '{}'::jsonb");
                    table.CheckConstraint("ck_notification_preferences_channels", "jsonb_typeof(channels) = 'object' AND (channels - ARRAY['Web', 'Email']::text[]) = '{}'::jsonb");
                    table.CheckConstraint("ck_notification_preferences_not_default", "NOT (channels = '{}'::jsonb AND categories = '{}'::jsonb)");
                });

            // R4, R11, U3: the table belongs to a user, as email_preferences does. Row level security, forced, shows the application only
            // the rows of the user the transaction declares.
            migrationBuilder.Sql("""
                ALTER TABLE notifications.notification_preferences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE notifications.notification_preferences FORCE ROW LEVEL SECURITY;
                CREATE POLICY own_notification_preferences ON notifications.notification_preferences TO api_application
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);

            // Each stored email choice moves to the email column: the main switch off, and each category whose email is on. Every
            // category's email was off by default and still is, so only a choice that is on is kept. The copy reads and writes every
            // user's rows, and the policies of both tables admit the application role only, so this transaction lifts the force of both for the one
            // statement and sets it again (R4).
            migrationBuilder.Sql("""
                ALTER TABLE notifications.email_preferences NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE notifications.notification_preferences NO FORCE ROW LEVEL SECURITY;
                INSERT INTO notifications.notification_preferences (user_id, channels, categories)
                SELECT preference.user_id,
                    CASE WHEN preference.enabled THEN '{}'::jsonb ELSE jsonb_build_object('Email', false) END,
                    coalesce(
                        (SELECT jsonb_object_agg(choice.key, jsonb_build_object('Email', true))
                         FROM jsonb_each(preference.categories) AS choice
                         WHERE choice.value = 'true'::jsonb),
                        '{}'::jsonb)
                FROM notifications.email_preferences AS preference
                WHERE NOT preference.enabled
                    OR EXISTS (SELECT FROM jsonb_each(preference.categories) AS choice WHERE choice.value = 'true'::jsonb);
                ALTER TABLE notifications.email_preferences FORCE ROW LEVEL SECURITY;
                ALTER TABLE notifications.notification_preferences FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_preferences",
                schema: "notifications");
        }
    }
}
