using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
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
                name: "ix_entries_tenant_id",
                schema: "audit",
                table: "entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_entries_tenant_id",
                schema: "audit",
                table: "entries",
                column: "tenant_id");
        }
    }
}
