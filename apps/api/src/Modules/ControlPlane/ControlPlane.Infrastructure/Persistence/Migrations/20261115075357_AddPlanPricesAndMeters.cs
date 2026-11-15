using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanPricesAndMeters : Migration
    {
        private static readonly Guid Free = new("0199c000-0000-7000-8000-00000000f4ee");

        private static readonly Guid Business = new("0199c000-0000-7000-8000-000000000940");

        private static readonly Guid Team = new("0199c000-0000-7000-8000-0000000007ea");

        private static readonly DateTimeOffset SeededAt = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "base_monthly",
                schema: "control_plane",
                table: "plans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "base_yearly",
                schema: "control_plane",
                table: "plans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "per_person_monthly",
                schema: "control_plane",
                table: "plans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "per_person_yearly",
                schema: "control_plane",
                table: "plans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "plan_meters",
                schema: "control_plane",
                columns: table => new
                {
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quota = table.Column<long>(type: "bigint", nullable: false),
                    overage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price = table.Column<long>(type: "bigint", nullable: false),
                    unit_size = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_meters", x => new { x.plan_id, x.key });
                    table.CheckConstraint("ck_plan_meters_overage", "overage IN ('Stop', 'Charge')");
                    table.CheckConstraint("ck_plan_meters_ranges", "quota BETWEEN 0 AND 1000000000000 AND unit_price BETWEEN 0 AND 10000000000 AND unit_size BETWEEN 1 AND 1000000000000");
                    table.ForeignKey(
                        name: "fk_plan_meters_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "control_plane",
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_plans_prices",
                schema: "control_plane",
                table: "plans",
                sql: "base_monthly BETWEEN 0 AND 10000000000 AND per_person_monthly BETWEEN 0 AND 10000000000 AND base_yearly BETWEEN 0 AND 10000000000 AND per_person_yearly BETWEEN 0 AND 10000000000");

            // Step 40's seeded plans (the maintainer's decision of 2026-11-15, docs/roadmap.md, section 8): Free with 3 seats, Team with 15
            // seats and 5 custom roles, and Business with 50 seats and 20 custom roles. Pro becomes Business, so a new and an existing
            // database hold the same three plans; this takes Pro's unlimited members away, on purpose, and a tenant over 50 keeps
            // everything (E4). A plan a system admin named Team or Business keeps its name with " (old)" after it, so the names stay
            // unique. The prices stay 0. The plans belong to no tenant (R6), so the statements declare nobody.
            migrationBuilder.Sql($"""
                UPDATE control_plane.plans SET name = left(name, 94) || ' (old)'
                WHERE lower(name) IN ('team', 'business') AND id NOT IN ('{Business}', '{Team}');
                UPDATE control_plane.plans SET name = 'Business' WHERE id = '{Business}';
                INSERT INTO control_plane.plans (id, name, created_at) VALUES ('{Team}', 'Team', '{SeededAt:O}');
                INSERT INTO control_plane.plan_entitlements (plan_id, key, enabled, maximum, unlimited) VALUES
                    ('{Team}', 'roles.custom', true, NULL, false),
                    ('{Team}', 'roles.custom.max', NULL, 5, false),
                    ('{Team}', 'members.max', NULL, 15, false),
                    ('{Team}', 'audit.retention.days', NULL, 365, false);
                UPDATE control_plane.plan_entitlements SET maximum = 50, unlimited = false WHERE plan_id = '{Business}' AND key = 'members.max';
                UPDATE control_plane.plan_entitlements SET maximum = 3, unlimited = false WHERE plan_id = '{Free}' AND key = 'members.max';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The seeded plans as the version before had them. Team goes only while no tenant is on it and it is not the default plan; else
            // the Down stops, as such a plan cannot be deleted (R16).
            migrationBuilder.Sql($"""
                UPDATE control_plane.plan_entitlements SET maximum = 5, unlimited = false WHERE plan_id = '{Free}' AND key = 'members.max';
                UPDATE control_plane.plan_entitlements SET maximum = NULL, unlimited = true WHERE plan_id = '{Business}' AND key = 'members.max';
                UPDATE control_plane.plans SET name = 'Pro' WHERE id = '{Business}';
                DELETE FROM control_plane.plan_entitlements WHERE plan_id = '{Team}';
                DELETE FROM control_plane.plans WHERE id = '{Team}';
                """);

            migrationBuilder.DropTable(
                name: "plan_meters",
                schema: "control_plane");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plans_prices",
                schema: "control_plane",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "base_monthly",
                schema: "control_plane",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "base_yearly",
                schema: "control_plane",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "per_person_monthly",
                schema: "control_plane",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "per_person_yearly",
                schema: "control_plane",
                table: "plans");
        }
    }
}
