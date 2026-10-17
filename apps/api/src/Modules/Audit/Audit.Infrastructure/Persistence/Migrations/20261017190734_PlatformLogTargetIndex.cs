using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformLogTargetIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_platform_entries_target_kind_target_id_occurred_at_id",
                schema: "audit",
                table: "platform_entries",
                columns: new[] { "target_kind", "target_id", "occurred_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_platform_entries_target_kind_target_id_occurred_at_id",
                schema: "audit",
                table: "platform_entries");
        }
    }
}
