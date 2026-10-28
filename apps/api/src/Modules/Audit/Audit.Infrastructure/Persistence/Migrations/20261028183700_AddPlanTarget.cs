using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_platform_entries_target_kind",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entries_target_kind",
                schema: "audit",
                table: "entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_platform_entries_target_kind",
                schema: "audit",
                table: "platform_entries",
                sql: "target_kind IN ('Tenant', 'User', 'Invitation', 'Role', 'Plan')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entries_target_kind",
                schema: "audit",
                table: "entries",
                sql: "target_kind IN ('Tenant', 'User', 'Invitation', 'Role', 'Plan')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_platform_entries_target_kind",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entries_target_kind",
                schema: "audit",
                table: "entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_platform_entries_target_kind",
                schema: "audit",
                table: "platform_entries",
                sql: "target_kind IN ('Tenant', 'User', 'Invitation', 'Role')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entries_target_kind",
                schema: "audit",
                table: "entries",
                sql: "target_kind IN ('Tenant', 'User', 'Invitation', 'Role')");
        }
    }
}
