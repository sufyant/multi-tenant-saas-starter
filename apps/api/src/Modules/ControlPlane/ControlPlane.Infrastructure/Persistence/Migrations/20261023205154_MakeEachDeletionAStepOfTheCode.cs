using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeEachDeletionAStepOfTheCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R16: a deletion of a tenant, a user or a role in use is refused, so each deletion is a step of the code. Nothing deletes a tenant or
            // a user row, and the tenant purge deletes the invitations and the memberships before the roles (section 6, "Tenant purge"), so
            // the code needs no change. The version before it deletes the same rows in the same order, so it still serves during the deploy.
            migrationBuilder.DropForeignKey(
                name: "fk_invitations_tenants_tenant_id",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.DropForeignKey(
                name: "fk_memberships_tenants_tenant_id",
                schema: "control_plane",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_memberships_users_user_id",
                schema: "control_plane",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_roles_tenants_tenant_id",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "fk_system_admins_users_user_id",
                schema: "control_plane",
                table: "system_admins");

            migrationBuilder.AddForeignKey(
                name: "fk_invitations_tenants_tenant_id",
                schema: "control_plane",
                table: "invitations",
                column: "tenant_id",
                principalSchema: "control_plane",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_memberships_tenants_tenant_id",
                schema: "control_plane",
                table: "memberships",
                column: "tenant_id",
                principalSchema: "control_plane",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_memberships_users_user_id",
                schema: "control_plane",
                table: "memberships",
                column: "user_id",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_roles_tenants_tenant_id",
                schema: "control_plane",
                table: "roles",
                column: "tenant_id",
                principalSchema: "control_plane",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_system_admins_users_user_id",
                schema: "control_plane",
                table: "system_admins",
                column: "user_id",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_invitations_tenants_tenant_id",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.DropForeignKey(
                name: "fk_memberships_tenants_tenant_id",
                schema: "control_plane",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_memberships_users_user_id",
                schema: "control_plane",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_roles_tenants_tenant_id",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "fk_system_admins_users_user_id",
                schema: "control_plane",
                table: "system_admins");

            migrationBuilder.AddForeignKey(
                name: "fk_invitations_tenants_tenant_id",
                schema: "control_plane",
                table: "invitations",
                column: "tenant_id",
                principalSchema: "control_plane",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_memberships_tenants_tenant_id",
                schema: "control_plane",
                table: "memberships",
                column: "tenant_id",
                principalSchema: "control_plane",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_memberships_users_user_id",
                schema: "control_plane",
                table: "memberships",
                column: "user_id",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_roles_tenants_tenant_id",
                schema: "control_plane",
                table: "roles",
                column: "tenant_id",
                principalSchema: "control_plane",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_system_admins_users_user_id",
                schema: "control_plane",
                table: "system_admins",
                column: "user_id",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
