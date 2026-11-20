using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "nearly_reached_period",
                schema: "control_plane",
                table: "spending_caps",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "nearly_used_at",
                schema: "control_plane",
                table: "meter_usages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_spending_caps_nearly_reached",
                schema: "control_plane",
                table: "spending_caps",
                sql: "(nearly_reached_period IS NULL OR extract(day FROM nearly_reached_period) = 1) AND (reached_period IS NULL OR nearly_reached_period IS NOT DISTINCT FROM reached_period)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_meter_usages_notices",
                schema: "control_plane",
                table: "meter_usages",
                sql: "quota_reached_at IS NULL OR nearly_used_at IS NOT NULL AND nearly_used_at <= quota_reached_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_spending_caps_nearly_reached",
                schema: "control_plane",
                table: "spending_caps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_meter_usages_notices",
                schema: "control_plane",
                table: "meter_usages");

            migrationBuilder.DropColumn(
                name: "nearly_reached_period",
                schema: "control_plane",
                table: "spending_caps");

            migrationBuilder.DropColumn(
                name: "nearly_used_at",
                schema: "control_plane",
                table: "meter_usages");
        }
    }
}
