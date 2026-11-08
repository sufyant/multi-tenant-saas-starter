using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TieWhoAndWhenToTheirEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 37b-backend-contract, the contract of step 37b's expansion (docs/deployment.md, section 8). The version before step 37b
            // ran during that deploy and changed the old columns without the new ones. The values it left belong to an event that is over,
            // so they go first; then the CHECKs tie the new columns to the old ones (R15).
            migrationBuilder.Sql(
                """
                UPDATE control_plane.tenants SET suspended_at = NULL, suspended_by = NULL WHERE suspension_reason IS NULL;
                UPDATE control_plane.tenants SET closed_by = NULL WHERE closed_at IS NULL;
                UPDATE control_plane.tenants SET purge_started_at = NULL WHERE status <> 'Purging';
                UPDATE control_plane.tenants SET created_by = NULL WHERE status = 'Purged';
                UPDATE control_plane.users SET suspension_reason = NULL WHERE suspended_at IS NULL OR deleted_at IS NOT NULL;
                UPDATE control_plane.tenant_summaries SET owner_invitation_sent_at = NULL WHERE owner_invitation_status IS NULL;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_purged_is_a_tombstone",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_suspension",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users",
                sql: "deleted_at IS NULL OR (email IS NULL AND name IS NULL AND time_zone IS NULL AND suspension_reason IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_suspension_reason_while_suspended",
                schema: "control_plane",
                table: "users",
                sql: "suspended_at IS NOT NULL OR suspension_reason IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_closed_by_with_closure",
                schema: "control_plane",
                table: "tenants",
                sql: "closed_by IS NULL OR closed_at IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_purge_started_while_purging",
                schema: "control_plane",
                table: "tenants",
                sql: "purge_started_at IS NULL OR status = 'Purging'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_purged_is_a_tombstone",
                schema: "control_plane",
                table: "tenants",
                sql: "status <> 'Purged' OR (purged_at IS NOT NULL AND closed_at IS NULL AND suspension_reason IS NULL AND created_by IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_suspension",
                schema: "control_plane",
                table: "tenants",
                sql: "(suspended_at IS NULL) = (suspended_by IS NULL) AND (suspended_at IS NULL OR suspension_reason IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_sent_with_it",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "owner_invitation_sent_at IS NULL OR owner_invitation_status IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The values the Up step cleared belong to an event that is over, so they do not come back.
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_suspension_reason_while_suspended",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_closed_by_with_closure",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_purge_started_while_purging",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_purged_is_a_tombstone",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_suspension",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_sent_with_it",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users",
                sql: "deleted_at IS NULL OR (email IS NULL AND name IS NULL AND time_zone IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_purged_is_a_tombstone",
                schema: "control_plane",
                table: "tenants",
                sql: "status <> 'Purged' OR (purged_at IS NOT NULL AND closed_at IS NULL AND suspension_reason IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_suspension",
                schema: "control_plane",
                table: "tenants",
                sql: "(suspended_at IS NULL) = (suspended_by IS NULL)");
        }
    }
}
