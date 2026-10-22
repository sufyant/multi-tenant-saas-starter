using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A3: a custom role's permissions are rows of role_permissions, a tenant table under the standard policy (R1,
            // TenantMigrationsSqlGenerator). A built-in role has none. The rows go with their role (ON DELETE CASCADE).
            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "control_plane",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.tenant_id, x.role_id, x.permission });
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_tenant_id_role_id",
                        columns: x => new { x.tenant_id, x.role_id },
                        principalSchema: "control_plane",
                        principalTable: "roles",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                schema: "control_plane",
                table: "roles",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name_trgm",
                schema: "control_plane",
                table: "roles",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            // A3: a role's name is unique in its tenant without regard to case, built-in names included. EF Core cannot model an index on
            // an expression, so it is written here (RoleConfiguration).
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_roles_tenant_id_lower_name ON control_plane.roles (tenant_id, lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX control_plane.ix_roles_tenant_id_lower_name;");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "control_plane");

            migrationBuilder.DropIndex(
                name: "ix_roles_name",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_name_trgm",
                schema: "control_plane",
                table: "roles");
        }
    }
}
