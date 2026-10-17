using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageAndTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every user and invitation before step 20j is English, as its email was. The default stays: the version that serves during
            // the deploy writes users and invitations without a language, and they get English too (docs/deployment.md, expand first).
            migrationBuilder.AddColumn<string>(
                name: "language",
                schema: "control_plane",
                table: "users",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "time_zone",
                schema: "control_plane",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language",
                schema: "control_plane",
                table: "invitations",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "language",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "time_zone",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "language",
                schema: "control_plane",
                table: "invitations");
        }
    }
}
