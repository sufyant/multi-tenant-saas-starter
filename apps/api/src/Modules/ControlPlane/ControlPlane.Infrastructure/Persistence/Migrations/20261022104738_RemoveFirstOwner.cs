using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFirstOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tenants_users_first_owner_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_first_owner_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "first_owner_id",
                schema: "control_plane",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "first_owner_id",
                schema: "control_plane",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_first_owner_id",
                schema: "control_plane",
                table: "tenants",
                column: "first_owner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_users_first_owner_id",
                schema: "control_plane",
                table: "tenants",
                column: "first_owner_id",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
