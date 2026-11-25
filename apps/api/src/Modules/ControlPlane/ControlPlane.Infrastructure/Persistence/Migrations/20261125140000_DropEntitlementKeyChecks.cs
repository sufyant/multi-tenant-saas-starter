using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropEntitlementKeyChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlement_overrides_key",
                schema: "control_plane",
                table: "entitlement_overrides");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlement_overrides_value_of_its_kind",
                schema: "control_plane",
                table: "entitlement_overrides");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_key",
                schema: "control_plane",
                table: "plan_entitlements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_value_of_its_kind",
                schema: "control_plane",
                table: "plan_entitlements");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entitlement_overrides_one_value",
                schema: "control_plane",
                table: "entitlement_overrides",
                sql: "(enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) OR (enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plan_entitlements_one_value",
                schema: "control_plane",
                table: "plan_entitlements",
                sql: "(enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) OR (enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlement_overrides_one_value",
                schema: "control_plane",
                table: "entitlement_overrides");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_one_value",
                schema: "control_plane",
                table: "plan_entitlements");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entitlement_overrides_key",
                schema: "control_plane",
                table: "entitlement_overrides",
                sql: "key IN ('audit.retention.days', 'members.max', 'roles.custom', 'roles.custom.max')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entitlement_overrides_value_of_its_kind",
                schema: "control_plane",
                table: "entitlement_overrides",
                sql: "(key IN ('roles.custom') AND enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) OR (key IN ('audit.retention.days', 'members.max', 'roles.custom.max') AND enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plan_entitlements_key",
                schema: "control_plane",
                table: "plan_entitlements",
                sql: "key IN ('audit.retention.days', 'members.max', 'roles.custom', 'roles.custom.max')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plan_entitlements_value_of_its_kind",
                schema: "control_plane",
                table: "plan_entitlements",
                sql: "(key IN ('roles.custom') AND enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) OR (key IN ('audit.retention.days', 'members.max', 'roles.custom.max') AND enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)");
        }
    }
}
