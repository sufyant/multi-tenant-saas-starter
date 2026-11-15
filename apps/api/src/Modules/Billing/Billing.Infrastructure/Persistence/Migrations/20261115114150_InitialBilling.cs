using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            // Privileges come from migrations. A customer and a subscription are written and deleted (the tenant purge); a received event
            // is only added and read.
            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA billing TO api_application;
                """);

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "billing",
                columns: table => new
                {
                    customer_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkout_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.customer_id);
                });

            migrationBuilder.CreateTable(
                name: "stripe_events",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stripe_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "billing",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    customer_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    subscription_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interval = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.tenant_id);
                    table.CheckConstraint("ck_subscriptions_interval", "interval IN ('Monthly', 'Yearly')");
                    table.CheckConstraint("ck_subscriptions_status", "status IN ('Incomplete', 'IncompleteExpired', 'Trialing', 'Active', 'PastDue', 'Canceled', 'Unpaid', 'Paused')");
                });

            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON billing.customers TO api_application;
                GRANT SELECT, INSERT ON billing.stripe_events TO api_application;
                GRANT SELECT, INSERT, UPDATE, DELETE ON billing.subscriptions TO api_application;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_customers_tenant_id",
                schema: "billing",
                table: "customers",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_customer_id",
                schema: "billing",
                table: "subscriptions",
                column: "customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_subscription_id",
                schema: "billing",
                table: "subscriptions",
                column: "subscription_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customers",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "stripe_events",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "billing");
        }
    }
}
