using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OneNotificationTypePerEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 33-backend-2: one type for each event, and a person kept by user id. Every row of the old types and values goes, as the
            // maintainer decided: no production holds one. Row level security, forced, binds the owner too (U8), so this transaction
            // lifts the force for the one deletion and sets it again.
            migrationBuilder.Sql(
                """
                ALTER TABLE notifications.notifications NO FORCE ROW LEVEL SECURITY;
                DELETE FROM notifications.notifications;
                ALTER TABLE notifications.notifications FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications",
                sql: "type IN ('member.joined', 'member.removed', 'member.role_changed', 'tenant.closed', 'tenant.entitlement_override_removed', 'tenant.entitlement_override_set', 'tenant.owner_joined', 'tenant.plan_changed', 'tenant.purged', 'tenant.reactivated', 'tenant.reopened', 'tenant.suspended')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications",
                sql: "type IN ('invitation.accepted', 'member.removed', 'member.role_changed', 'tenant.owner_joined', 'tenant.plan_changed', 'tenant.purged', 'tenant.status_changed')");
        }
    }
}
