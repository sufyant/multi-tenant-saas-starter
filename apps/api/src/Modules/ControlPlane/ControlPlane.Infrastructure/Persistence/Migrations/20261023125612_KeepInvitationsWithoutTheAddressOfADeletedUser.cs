using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KeepInvitationsWithoutTheAddressOfADeletedUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A8: a user's deletion erases the address of a finished invitation. Every invitation has an address before this migration, so
            // the CHECK holds at once. The column only widens, so the version before it still serves during the deploy (section 13).
            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "control_plane",
                table: "invitations",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_pending_has_email",
                schema: "control_plane",
                table: "invitations",
                sql: "status <> 'Pending' OR email IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_pending_has_email",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "control_plane",
                table: "invitations",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);
        }
    }
}
