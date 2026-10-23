using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KeyTheLogsByTheMomentAndTheId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R18, K3: each log is keyed by the moment and then the id, so a later retention can partition it by the moment. The key's index
            // replaces the platform's index on the same columns. The version before it still finds an entry by its id, without an index,
            // during the deploy only.
            migrationBuilder.DropPrimaryKey(
                name: "pk_platform_entries",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropIndex(
                name: "ix_platform_entries_occurred_at_id",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropPrimaryKey(
                name: "pk_entries",
                schema: "audit",
                table: "entries");

            migrationBuilder.AddPrimaryKey(
                name: "pk_platform_entries",
                schema: "audit",
                table: "platform_entries",
                columns: new[] { "occurred_at", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_entries",
                schema: "audit",
                table: "entries",
                columns: new[] { "occurred_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The key of the id alone refuses two entries with one id and two moments, which the new key allows.
            migrationBuilder.DropPrimaryKey(
                name: "pk_platform_entries",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropPrimaryKey(
                name: "pk_entries",
                schema: "audit",
                table: "entries");

            migrationBuilder.AddPrimaryKey(
                name: "pk_platform_entries",
                schema: "audit",
                table: "platform_entries",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_entries",
                schema: "audit",
                table: "entries",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_entries_occurred_at_id",
                schema: "audit",
                table: "platform_entries",
                columns: new[] { "occurred_at", "id" });
        }
    }
}
