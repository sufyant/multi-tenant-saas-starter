using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "api");
            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA api TO api_application;
                ALTER DEFAULT PRIVILEGES IN SCHEMA api GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO api_application;
                """);

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "api",
                columns: table => new
                {
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    route = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    lease = table.Column<Guid>(type: "uuid", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_keys", x => new { x.subject, x.key });
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_keys_created_at",
                schema: "api",
                table: "idempotency_keys",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "api");
            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES IN SCHEMA api REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM api_application;
                REVOKE USAGE ON SCHEMA api FROM api_application;
                """);
        }
    }
}
