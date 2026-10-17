using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClerkOnlySignsIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "profile_updated_at",
                schema: "control_plane",
                table: "users");

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

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "profile_updated_at",
                schema: "control_plane",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
