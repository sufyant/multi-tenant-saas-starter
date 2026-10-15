using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformLogAndTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "actor_id",
                schema: "audit",
                table: "entries",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "actor_kind",
                schema: "audit",
                table: "entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "target_id",
                schema: "audit",
                table: "entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_kind",
                schema: "audit",
                table: "entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_name",
                schema: "audit",
                table: "entries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_entries",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    operation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    target_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_entries_occurred_at",
                schema: "audit",
                table: "platform_entries",
                column: "occurred_at");

            // K3, R6, R11: the platform's log has no tenant column. The application role gets SELECT and INSERT only, from the
            // schema's default privileges. Row level security, forced, keeps it from a transaction that declares a tenant or a user:
            // such a transaction reads no row, and Audit, in a tenant's transaction, may still add the platform's record.
            migrationBuilder.Sql("""
                ALTER TABLE audit.platform_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE audit.platform_entries FORCE ROW LEVEL SECURITY;
                CREATE POLICY platform_read ON audit.platform_entries FOR SELECT
                    USING (NULLIF(current_setting('app.tenant_id', true), '') IS NULL AND NULLIF(current_setting('app.user_id', true), '') IS NULL);
                CREATE POLICY platform_insert ON audit.platform_entries FOR INSERT WITH CHECK (true);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_entries",
                schema: "audit");

            migrationBuilder.DropColumn(
                name: "actor_kind",
                schema: "audit",
                table: "entries");

            migrationBuilder.DropColumn(
                name: "target_id",
                schema: "audit",
                table: "entries");

            migrationBuilder.DropColumn(
                name: "target_kind",
                schema: "audit",
                table: "entries");

            migrationBuilder.DropColumn(
                name: "target_name",
                schema: "audit",
                table: "entries");

            migrationBuilder.AlterColumn<string>(
                name: "actor_id",
                schema: "audit",
                table: "entries",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
