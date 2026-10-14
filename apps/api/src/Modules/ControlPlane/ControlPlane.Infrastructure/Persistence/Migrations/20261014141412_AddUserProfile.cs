using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "control_plane",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "control_plane",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "control_plane",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "control_plane",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "profile_updated_at",
                schema: "control_plane",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "profile_updated_at",
                schema: "control_plane",
                table: "users");
        }
    }
}
