using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActorIdUuidAndCursorIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_platform_entries_occurred_at",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropIndex(
                name: "ix_entries_occurred_at",
                schema: "audit",
                table: "entries");

            // The tenant's log keeps the actor id as a uuid, as the platform's log does (K3). The generator writes no USING clause, and
            // PostgreSQL casts text to uuid only with one. A value that is not a uuid fails the cast, and the migration's transaction
            // rolls back with the column unchanged. The change is in place: the previous version writes this column as text, so its
            // audit records fail for the seconds of a deploy until the new version runs (accepted: no environment holds real data).
            migrationBuilder.Sql("ALTER TABLE audit.entries ALTER COLUMN actor_id TYPE uuid USING actor_id::uuid;");

            migrationBuilder.CreateIndex(
                name: "ix_platform_entries_occurred_at_id",
                schema: "audit",
                table: "platform_entries",
                columns: new[] { "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_entries_tenant_id_occurred_at_id",
                schema: "audit",
                table: "entries",
                columns: new[] { "tenant_id", "occurred_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_platform_entries_occurred_at_id",
                schema: "audit",
                table: "platform_entries");

            migrationBuilder.DropIndex(
                name: "ix_entries_tenant_id_occurred_at_id",
                schema: "audit",
                table: "entries");

            migrationBuilder.AlterColumn<string>(
                name: "actor_id",
                schema: "audit",
                table: "entries",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_entries_occurred_at",
                schema: "audit",
                table: "platform_entries",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_entries_occurred_at",
                schema: "audit",
                table: "entries",
                column: "occurred_at");
        }
    }
}
