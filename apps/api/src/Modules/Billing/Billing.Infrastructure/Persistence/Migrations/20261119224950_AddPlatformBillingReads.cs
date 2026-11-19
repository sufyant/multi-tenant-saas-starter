using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformBillingReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_paid_at",
                schema: "billing",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "overdue",
                schema: "billing",
                table: "payers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "contract_tenants",
                schema: "billing",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_tenants", x => x.tenant_id);
                });

            // R6: the listing belongs to no tenant, as the payers do. The migration fills it and the overdue mark from the rows it derives
            // from (R4's exception for derived data).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, DELETE ON billing.contract_tenants TO api_application;
                INSERT INTO billing.contract_tenants (tenant_id) SELECT tenant_id FROM billing.contracts ON CONFLICT DO NOTHING;
                UPDATE billing.payers AS payer SET overdue = true
                FROM billing.subscriptions AS subscription
                WHERE subscription.tenant_id = payer.tenant_id AND subscription.failed_payment_invoice_id IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM billing.contracts AS contract WHERE contract.tenant_id = payer.tenant_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contract_tenants",
                schema: "billing");

            migrationBuilder.DropColumn(
                name: "last_paid_at",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "overdue",
                schema: "billing",
                table: "payers");
        }
    }
}
