using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnersStatusTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_tenant",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_tenant",
                schema: "notifications",
                table: "notifications",
                sql: "(bell <> 'Tenant' OR tenant_id IS NOT NULL) AND (bell <> 'Personal' OR (tenant_id IS NOT NULL) = (type IN ('tenant.reactivated_for_owners', 'tenant.reopened_for_owners')))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications",
                sql: "type IN ('member.joined', 'member.removed', 'member.role_changed', 'tenant.closed', 'tenant.closed_for_owners', 'tenant.entitlement_override_removed', 'tenant.entitlement_override_set', 'tenant.owner_joined', 'tenant.plan_changed', 'tenant.purged', 'tenant.purged_for_owners', 'tenant.reactivated', 'tenant.reactivated_for_owners', 'tenant.reopened', 'tenant.reopened_for_owners', 'tenant.suspended', 'tenant.suspended_for_owners', 'user.data_export_ready')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_tenant",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_tenant",
                schema: "notifications",
                table: "notifications",
                sql: "(bell <> 'Tenant' OR tenant_id IS NOT NULL) AND (bell <> 'Personal' OR tenant_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications",
                sql: "type IN ('member.joined', 'member.removed', 'member.role_changed', 'tenant.closed', 'tenant.entitlement_override_removed', 'tenant.entitlement_override_set', 'tenant.owner_joined', 'tenant.plan_changed', 'tenant.purged', 'tenant.reactivated', 'tenant.reopened', 'tenant.suspended', 'user.data_export_ready')");
        }
    }
}
