using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "control_plane",
                table: "users",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_users_email_trgm",
                schema: "control_plane",
                table: "users",
                column: "email")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_first_name_trgm",
                schema: "control_plane",
                table: "users",
                column: "first_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_last_name",
                schema: "control_plane",
                table: "users",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "ix_users_last_name_trgm",
                schema: "control_plane",
                table: "users",
                column: "last_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_tenants_name",
                schema: "control_plane",
                table: "tenants",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_name_trgm",
                schema: "control_plane",
                table: "tenants",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug_trgm",
                schema: "control_plane",
                table: "tenants",
                column: "slug")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_email",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_email_trgm",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_first_name_trgm",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_last_name",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_last_name_trgm",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_tenants_name",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_name_trgm",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_slug_trgm",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
