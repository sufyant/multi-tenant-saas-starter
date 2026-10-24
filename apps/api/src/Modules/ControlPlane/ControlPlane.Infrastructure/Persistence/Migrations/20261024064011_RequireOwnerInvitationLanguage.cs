using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireOwnerInvitationLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_together",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_together",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "(owner_invitation_status IS NULL) = (owner_invitation_expires_at IS NULL) AND (owner_invitation_status IS NULL) = (owner_invitation_language IS NULL) AND (owner_invitation_status IS NOT NULL OR owner_invitation_email IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_together",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_owner_invitation_together",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "(owner_invitation_status IS NULL) = (owner_invitation_expires_at IS NULL) AND (owner_invitation_status IS NOT NULL OR (owner_invitation_email IS NULL AND owner_invitation_language IS NULL))");
        }
    }
}
