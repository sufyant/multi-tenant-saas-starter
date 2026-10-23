using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R15: single-row rules of the code, which the database now keeps too. No row that the code writes breaks one, and no path of an
            // earlier version wrote one (MigrationStepTests), so no row needs a cleanup. The version before it writes the same rows, so it
            // still serves during the deploy (section 13).
            migrationBuilder.AddCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users",
                sql: "deleted_at IS NULL OR (email IS NULL AND first_name IS NULL AND last_name IS NULL AND time_zone IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_language",
                schema: "control_plane",
                table: "users",
                sql: "language IN ('en', 'tr', 'ar')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_membership_count",
                schema: "control_plane",
                table: "users",
                sql: "membership_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_suspension",
                schema: "control_plane",
                table: "users",
                sql: "(suspended_at IS NULL) = (suspended_by IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_purged_is_a_tombstone",
                schema: "control_plane",
                table: "tenants",
                sql: "status <> 'Purged' OR (purged_at IS NOT NULL AND closed_at IS NULL AND suspension_reason IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_status",
                schema: "control_plane",
                table: "tenants",
                sql: "status IN ('Active', 'Suspended', 'Closed', 'Purging', 'Purged')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "member_count >= 0 AND owner_count >= 0 AND member_count >= owner_count");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_language",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "owner_invitation_language IN ('en', 'tr', 'ar')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_status",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "owner_invitation_status IN ('Pending', 'Accepted', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_together",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "(owner_invitation_status IS NULL) = (owner_invitation_expires_at IS NULL) AND (owner_invitation_status IS NOT NULL OR (owner_invitation_email IS NULL AND owner_invitation_language IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_without_owner",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "owner_count = 0 OR owner_invitation_status IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_pending_owner_invitation_has_email",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "owner_invitation_status <> 'Pending' OR owner_invitation_email IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_purge_sagas_state",
                schema: "control_plane",
                table: "tenant_purge_sagas",
                sql: "state IN ('PurgingModules', 'Finishing', 'Completed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_acceptance",
                schema: "control_plane",
                table: "invitations",
                sql: "(accepted_at IS NULL) = (accepted_by IS NULL) AND (status = 'Accepted') = (accepted_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_language",
                schema: "control_plane",
                table: "invitations",
                sql: "language IN ('en', 'tr', 'ar')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_status",
                schema: "control_plane",
                table: "invitations",
                sql: "status IN ('Pending', 'Accepted', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_language",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_membership_count",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_suspension",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_purged_is_a_tombstone",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_status",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_language",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_status",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_together",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_without_owner",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_pending_owner_invitation_has_email",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_purge_sagas_state",
                schema: "control_plane",
                table: "tenant_purge_sagas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_acceptance",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_language",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_status",
                schema: "control_plane",
                table: "invitations");
        }
    }
}
