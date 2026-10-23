using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R15: an entry's actor, kind and target are of the kinds the code knows. Every entry the code wrote is, so no row needs a
            // cleanup, and the version before it still serves during the deploy (section 13).
            migrationBuilder.AddCheckConstraint(
                name: "ck_platform_entries_actor_kind",
                schema: "audit",
                table: "platform_entries",
                sql: "actor_kind IN ('User', 'Platform')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_platform_entries_kind",
                schema: "audit",
                table: "platform_entries",
                sql: "kind IN ('Command')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_platform_entries_target_kind",
                schema: "audit",
                table: "platform_entries",
                sql: "target_kind IN ('Tenant', 'User', 'Invitation', 'Role')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entries_actor_kind",
                schema: "audit",
                table: "entries",
                sql: "actor_kind IN ('User', 'Platform')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entries_kind",
                schema: "audit",
                table: "entries",
                sql: "kind IN ('Command')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entries_target_kind",
                schema: "audit",
                table: "entries",
                sql: "target_kind IN ('Tenant', 'User', 'Invitation', 'Role')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_platform_entries_actor_kind",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_platform_entries_kind",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_platform_entries_target_kind",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entries_actor_kind",
                schema: "audit",
                table: "entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entries_kind",
                schema: "audit",
                table: "entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entries_target_kind",
                schema: "audit",
                table: "entries");
        }
    }
}
