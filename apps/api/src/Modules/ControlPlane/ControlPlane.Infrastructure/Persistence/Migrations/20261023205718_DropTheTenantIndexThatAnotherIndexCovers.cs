using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropTheTenantIndexThatAnotherIndexCovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R1: a key or another index leads with the tenant column, so the index of the tenant column alone only cost each write. The
            // version before it reads through the same leading index, so it still serves during the deploy.
            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_invitations_tenant_id",
                schema: "control_plane",
                table: "invitations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id",
                schema: "control_plane",
                table: "roles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_tenant_id",
                schema: "control_plane",
                table: "invitations",
                column: "tenant_id");
        }
    }
}
