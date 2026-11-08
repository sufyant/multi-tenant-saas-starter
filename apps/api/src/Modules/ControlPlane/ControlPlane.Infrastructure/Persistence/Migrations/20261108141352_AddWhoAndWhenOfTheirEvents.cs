using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWhoAndWhenOfTheirEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "suspension_reason",
                schema: "control_plane",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "closed_by",
                schema: "control_plane",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "control_plane",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "purge_started_at",
                schema: "control_plane",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "suspended_at",
                schema: "control_plane",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "suspended_by",
                schema: "control_plane",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "owner_invitation_sent_at",
                schema: "control_plane",
                table: "tenant_summaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "granted_by",
                schema: "control_plane",
                table: "system_admins",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_closed_by",
                schema: "control_plane",
                table: "tenants",
                column: "closed_by");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_created_by",
                schema: "control_plane",
                table: "tenants",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_suspended_by",
                schema: "control_plane",
                table: "tenants",
                column: "suspended_by");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_suspension",
                schema: "control_plane",
                table: "tenants",
                sql: "(suspended_at IS NULL) = (suspended_by IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_system_admins_granted_by",
                schema: "control_plane",
                table: "system_admins",
                column: "granted_by");

            migrationBuilder.AddForeignKey(
                name: "fk_system_admins_users_granted_by",
                schema: "control_plane",
                table: "system_admins",
                column: "granted_by",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_users_closed_by",
                schema: "control_plane",
                table: "tenants",
                column: "closed_by",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_users_created_by",
                schema: "control_plane",
                table: "tenants",
                column: "created_by",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_users_suspended_by",
                schema: "control_plane",
                table: "tenants",
                column: "suspended_by",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_system_admins_users_granted_by",
                schema: "control_plane",
                table: "system_admins");

            migrationBuilder.DropForeignKey(
                name: "fk_tenants_users_closed_by",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropForeignKey(
                name: "fk_tenants_users_created_by",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropForeignKey(
                name: "fk_tenants_users_suspended_by",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_closed_by",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_created_by",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_suspended_by",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_suspension",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_system_admins_granted_by",
                schema: "control_plane",
                table: "system_admins");

            migrationBuilder.DropColumn(
                name: "suspension_reason",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "closed_by",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "purge_started_at",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "suspended_at",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "suspended_by",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_invitation_sent_at",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropColumn(
                name: "granted_by",
                schema: "control_plane",
                table: "system_admins");
        }
    }
}
