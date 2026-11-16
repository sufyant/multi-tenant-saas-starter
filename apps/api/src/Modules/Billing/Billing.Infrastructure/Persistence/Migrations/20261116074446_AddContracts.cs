using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contracts",
                schema: "billing",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contracts", x => x.tenant_id);
                });

            // Privileges come from migrations: the application marks a tenant Contract and takes the mark back (B12). UPDATE is for the lock
            // of SELECT ... FOR UPDATE, which PostgreSQL grants only with it; the code changes no row.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON billing.contracts TO api_application;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contracts",
                schema: "billing");
        }
    }
}
