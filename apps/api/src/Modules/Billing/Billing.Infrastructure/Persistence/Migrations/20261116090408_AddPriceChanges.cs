using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payers",
                schema: "billing",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    yearly = table.Column<bool>(type: "boolean", nullable: false),
                    base_amount = table.Column<long>(type: "bigint", nullable: false),
                    per_person_amount = table.Column<long>(type: "bigint", nullable: false),
                    ends = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payers", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "price_changes",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    moves_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    new_base_monthly = table.Column<long>(type: "bigint", nullable: false),
                    new_base_yearly = table.Column<long>(type: "bigint", nullable: false),
                    new_per_person_monthly = table.Column<long>(type: "bigint", nullable: false),
                    new_per_person_yearly = table.Column<long>(type: "bigint", nullable: false),
                    old_base_monthly = table.Column<long>(type: "bigint", nullable: false),
                    old_base_yearly = table.Column<long>(type: "bigint", nullable: false),
                    old_per_person_monthly = table.Column<long>(type: "bigint", nullable: false),
                    old_per_person_yearly = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_changes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payers_plan_id",
                schema: "billing",
                table: "payers",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_changes_plan_id_changed_at",
                schema: "billing",
                table: "price_changes",
                columns: new[] { "plan_id", "changed_at" });

            // Privileges come from migrations (B13). Both tables belong to no tenant (R6): the application keeps each plan edit's price change
            // once and gives it a day, and writes each tenant's payer with its subscription.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE ON billing.price_changes TO api_application;
                GRANT SELECT, INSERT, UPDATE, DELETE ON billing.payers TO api_application;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payers",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "price_changes",
                schema: "billing");
        }
    }
}
