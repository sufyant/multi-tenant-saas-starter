using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClerkOnlySignsIn : Migration
    {
        // Expand only: the version that serves during the deploy still reads users.profile_updated_at, so the column stays, unused
        // and nullable. A later release drops it (docs/deployment.md, section 8).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sign_up_invitation_id",
                schema: "control_plane",
                table: "invitations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sign_up_invitation_id",
                schema: "control_plane",
                table: "invitations");
        }
    }
}
