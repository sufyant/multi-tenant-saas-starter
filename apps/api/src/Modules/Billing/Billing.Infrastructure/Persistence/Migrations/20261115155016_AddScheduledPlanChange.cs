using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledPlanChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scheduled_interval",
                schema: "billing",
                table: "subscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "scheduled_plan_id",
                schema: "billing",
                table: "subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_scheduled",
                schema: "billing",
                table: "subscriptions",
                sql: "(scheduled_plan_id IS NULL) = (scheduled_interval IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_scheduled_interval",
                schema: "billing",
                table: "subscriptions",
                sql: "scheduled_interval IN ('Monthly', 'Yearly')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_scheduled",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_scheduled_interval",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "scheduled_interval",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "scheduled_plan_id",
                schema: "billing",
                table: "subscriptions");
        }
    }
}
