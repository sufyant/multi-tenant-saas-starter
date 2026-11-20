using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTrials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "plan_before_trial_id",
                schema: "control_plane",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "trial_ends_at",
                schema: "control_plane",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trial_plan_id",
                schema: "control_plane",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "trial_started_at",
                schema: "control_plane",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_plan_before_trial_id",
                schema: "control_plane",
                table: "tenants",
                column: "plan_before_trial_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_trial_plan_id",
                schema: "control_plane",
                table: "tenants",
                column: "trial_plan_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_trial",
                schema: "control_plane",
                table: "tenants",
                sql: "(trial_plan_id IS NULL) = (trial_started_at IS NULL) AND (trial_plan_id IS NULL) = (trial_ends_at IS NULL) AND (plan_before_trial_id IS NULL OR trial_plan_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_plans_plan_before_trial_id",
                schema: "control_plane",
                table: "tenants",
                column: "plan_before_trial_id",
                principalSchema: "control_plane",
                principalTable: "plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_plans_trial_plan_id",
                schema: "control_plane",
                table: "tenants",
                column: "trial_plan_id",
                principalSchema: "control_plane",
                principalTable: "plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tenants_plans_plan_before_trial_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropForeignKey(
                name: "fk_tenants_plans_trial_plan_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_plan_before_trial_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_trial_plan_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_trial",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "plan_before_trial_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "trial_ends_at",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "trial_plan_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "trial_started_at",
                schema: "control_plane",
                table: "tenants");
        }
    }
}
