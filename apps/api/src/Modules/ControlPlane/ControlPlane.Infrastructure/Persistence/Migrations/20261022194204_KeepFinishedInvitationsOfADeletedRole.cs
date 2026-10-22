using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KeepFinishedInvitationsOfADeletedRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A pending invitation always has a role. Every invitation has one before this migration, so the CHECK holds at once.
            migrationBuilder.DropForeignKey(
                name: "fk_invitations_roles_tenant_id_role_id",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                schema: "control_plane",
                table: "invitations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_pending_has_role",
                schema: "control_plane",
                table: "invitations",
                sql: "status <> 'Pending' OR role_id IS NOT NULL");

            // A3: a custom role that a membership or a pending invitation uses cannot be deleted; a finished invitation still names its
            // role, and loses it when the role is deleted. SET NULL (role_id) nulls the role only and keeps the tenant column, which is not
            // null (PostgreSQL 15 and later). EF Core cannot write a column list here, so the key is written in SQL (InvitationConfiguration).
            migrationBuilder.Sql("""
                ALTER TABLE control_plane.invitations ADD CONSTRAINT fk_invitations_roles_tenant_id_role_id
                    FOREIGN KEY (tenant_id, role_id) REFERENCES control_plane.roles (tenant_id, id) ON DELETE SET NULL (role_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_invitations_roles_tenant_id_role_id",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_pending_has_role",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                schema: "control_plane",
                table: "invitations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_invitations_roles_tenant_id_role_id",
                schema: "control_plane",
                table: "invitations",
                columns: new[] { "tenant_id", "role_id" },
                principalSchema: "control_plane",
                principalTable: "roles",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
