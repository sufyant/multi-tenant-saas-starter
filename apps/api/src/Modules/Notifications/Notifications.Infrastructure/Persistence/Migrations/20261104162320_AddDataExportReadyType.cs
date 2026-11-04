using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataExportReadyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications",
                sql: "type IN ('member.joined', 'member.removed', 'member.role_changed', 'tenant.closed', 'tenant.entitlement_override_removed', 'tenant.entitlement_override_set', 'tenant.owner_joined', 'tenant.plan_changed', 'tenant.purged', 'tenant.reactivated', 'tenant.reopened', 'tenant.suspended', 'user.data_export_ready')");
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
                sql: "type IN ('member.joined', 'member.removed', 'member.role_changed', 'tenant.closed', 'tenant.entitlement_override_removed', 'tenant.entitlement_override_set', 'tenant.owner_joined', 'tenant.plan_changed', 'tenant.purged', 'tenant.reactivated', 'tenant.reopened', 'tenant.suspended')");
        }
    }
}
