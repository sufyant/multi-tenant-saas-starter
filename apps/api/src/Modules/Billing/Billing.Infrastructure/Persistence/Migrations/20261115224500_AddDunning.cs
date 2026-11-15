using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDunning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "failed_payment_amount_due",
                schema: "billing",
                table: "subscriptions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "failed_payment_attempts",
                schema: "billing",
                table: "subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failed_payment_card_brand",
                schema: "billing",
                table: "subscriptions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failed_payment_card_last4",
                schema: "billing",
                table: "subscriptions",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failed_payment_currency",
                schema: "billing",
                table: "subscriptions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "failed_payment_failed_at",
                schema: "billing",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failed_payment_invoice_id",
                schema: "billing",
                table: "subscriptions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "failed_payment_next_attempt_at",
                schema: "billing",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "grace_ends_at",
                schema: "billing",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "moved_to_default_at",
                schema: "billing",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_payment_amount_due",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "failed_payment_attempts",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "failed_payment_card_brand",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "failed_payment_card_last4",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "failed_payment_currency",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "failed_payment_failed_at",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "failed_payment_invoice_id",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "failed_payment_next_attempt_at",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "grace_ends_at",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "moved_to_default_at",
                schema: "billing",
                table: "subscriptions");
        }
    }
}
