using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "closure_end",
                schema: "billing",
                table: "subscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                schema: "billing",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lifecycle_at",
                schema: "billing",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "checkouts",
                schema: "billing",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    key = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    yearly = table.Column<bool>(type: "boolean", nullable: false),
                    people = table.Column<int>(type: "integer", nullable: false),
                    return_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    base_amount = table.Column<long>(type: "bigint", nullable: false),
                    per_person_amount = table.Column<long>(type: "bigint", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    session_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checkouts", x => x.tenant_id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_closure_end",
                schema: "billing",
                table: "subscriptions",
                sql: "closure_end IN ('None', 'Asked', 'Released')");

            // Privileges come from migrations: the application writes and deletes a tenant's open checkout (B7).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON billing.checkouts TO api_application;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkouts",
                schema: "billing");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_closure_end",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "closure_end",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "closed_at",
                schema: "billing",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "lifecycle_at",
                schema: "billing",
                table: "customers");
        }
    }
}
