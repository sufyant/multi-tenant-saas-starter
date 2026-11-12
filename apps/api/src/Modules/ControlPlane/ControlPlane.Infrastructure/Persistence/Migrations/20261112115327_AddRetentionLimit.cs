using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_key",
                schema: "control_plane",
                table: "plan_entitlements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_value_of_its_kind",
                schema: "control_plane",
                table: "plan_entitlements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlement_overrides_key",
                schema: "control_plane",
                table: "entitlement_overrides");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlement_overrides_value_of_its_kind",
                schema: "control_plane",
                table: "entitlement_overrides");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plan_entitlements_audit_retention_floor",
                schema: "control_plane",
                table: "plan_entitlements",
                sql: "key <> 'audit.retention.days' OR unlimited OR maximum >= 30");

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

            migrationBuilder.AddCheckConstraint(
                name: "ck_entitlement_overrides_audit_retention_floor",
                schema: "control_plane",
                table: "entitlement_overrides",
                sql: "key <> 'audit.retention.days' OR unlimited OR maximum >= 30");

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

            // K14: every plan keeps the Activity log for a year. The plans belong to no tenant (R6), so the statement declares nobody. A plan
            // that a system admin creates later gets the value its editor sends; a plan without one keeps every entry (E3).
            migrationBuilder.Sql("""
                INSERT INTO control_plane.plan_entitlements (plan_id, key, enabled, maximum, unlimited)
                SELECT id, 'audit.retention.days', NULL, 365, false FROM control_plane.plans
                ON CONFLICT (plan_id, key) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // E1: removing a key deletes its rows, before the constraints of the version before refuse them.
            migrationBuilder.Sql("""
                DELETE FROM control_plane.entitlement_overrides WHERE key = 'audit.retention.days';
                DELETE FROM control_plane.plan_entitlements WHERE key = 'audit.retention.days';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_audit_retention_floor",
                schema: "control_plane",
                table: "plan_entitlements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_key",
                schema: "control_plane",
                table: "plan_entitlements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_value_of_its_kind",
                schema: "control_plane",
                table: "plan_entitlements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlement_overrides_audit_retention_floor",
                schema: "control_plane",
                table: "entitlement_overrides");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlement_overrides_key",
                schema: "control_plane",
                table: "entitlement_overrides");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlement_overrides_value_of_its_kind",
                schema: "control_plane",
                table: "entitlement_overrides");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plan_entitlements_key",
                schema: "control_plane",
                table: "plan_entitlements",
                sql: "key IN ('members.max', 'roles.custom', 'roles.custom.max')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plan_entitlements_value_of_its_kind",
                schema: "control_plane",
                table: "plan_entitlements",
                sql: "(key IN ('roles.custom') AND enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) OR (key IN ('members.max', 'roles.custom.max') AND enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entitlement_overrides_key",
                schema: "control_plane",
                table: "entitlement_overrides",
                sql: "key IN ('members.max', 'roles.custom', 'roles.custom.max')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entitlement_overrides_value_of_its_kind",
                schema: "control_plane",
                table: "entitlement_overrides",
                sql: "(key IN ('roles.custom') AND enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) OR (key IN ('members.max', 'roles.custom.max') AND enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)");
        }
    }
}
