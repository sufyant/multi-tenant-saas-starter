using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LowerCaseRoleNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "control_plane",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-7000-8000-000000000001"),
                column: "name",
                value: "owner");

            migrationBuilder.UpdateData(
                schema: "control_plane",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-7000-8000-000000000002"),
                column: "name",
                value: "admin");

            migrationBuilder.UpdateData(
                schema: "control_plane",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-7000-8000-000000000003"),
                column: "name",
                value: "member");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "control_plane",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-7000-8000-000000000001"),
                column: "name",
                value: "Owner");

            migrationBuilder.UpdateData(
                schema: "control_plane",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-7000-8000-000000000002"),
                column: "name",
                value: "Admin");

            migrationBuilder.UpdateData(
                schema: "control_plane",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-7000-8000-000000000003"),
                column: "name",
                value: "Member");
        }
    }
}
