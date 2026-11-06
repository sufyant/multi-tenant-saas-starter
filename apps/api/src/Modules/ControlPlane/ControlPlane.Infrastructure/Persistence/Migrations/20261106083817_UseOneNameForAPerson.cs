using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseOneNameForAPerson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 36: a person has one name, never split (A7; the maintainer's decision of 2026-11-06). The new column comes first and takes
            // the first and the last name, joined by a space and trimmed. A user with neither, or with only spaces, keeps none. A joined name
            // over the length of the domain keeps its first 100 characters (R17). No production holds a user, so the old columns go in the
            // same migration (the maintainer's decision).
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "control_plane",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE control_plane.users
                SET name = NULLIF(btrim(left(concat_ws(' ', NULLIF(btrim(first_name), ''), NULLIF(btrim(last_name), '')), 100)), '')
                """);

            migrationBuilder.DropIndex(
                name: "ix_users_first_name_trgm",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_last_name_trgm",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_last_name",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "control_plane",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_name",
                schema: "control_plane",
                table: "users",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_users_name_trgm",
                schema: "control_plane",
                table: "users",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users",
                sql: "deleted_at IS NULL OR (email IS NULL AND name IS NULL AND time_zone IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The name is never split: the whole name goes back as the first name, and the last name stays empty.
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "control_plane",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "control_plane",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("UPDATE control_plane.users SET first_name = name");

            migrationBuilder.DropIndex(
                name: "ix_users_name",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_name_trgm",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "name",
                schema: "control_plane",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_last_name",
                schema: "control_plane",
                table: "users",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "ix_users_first_name_trgm",
                schema: "control_plane",
                table: "users",
                column: "first_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_last_name_trgm",
                schema: "control_plane",
                table: "users",
                column: "last_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_deleted_keeps_no_personal_data",
                schema: "control_plane",
                table: "users",
                sql: "deleted_at IS NULL OR (email IS NULL AND first_name IS NULL AND last_name IS NULL AND time_zone IS NULL)");
        }
    }
}
