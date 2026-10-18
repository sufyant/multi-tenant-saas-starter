using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameInvitationSecretHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "token_hash",
                schema: "control_plane",
                table: "invitations",
                newName: "secret_hash");

            migrationBuilder.RenameIndex(
                name: "ix_invitations_token_hash",
                schema: "control_plane",
                table: "invitations",
                newName: "ix_invitations_secret_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "secret_hash",
                schema: "control_plane",
                table: "invitations",
                newName: "token_hash");

            migrationBuilder.RenameIndex(
                name: "ix_invitations_secret_hash",
                schema: "control_plane",
                table: "invitations",
                newName: "ix_invitations_token_hash");
        }
    }
}
