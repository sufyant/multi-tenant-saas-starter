using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPurgeSagas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // W6, S3: the tenant purge's record, a tenant table, which the migrations' SQL generator puts under row level security (R1).
            // A new table, so the version before it still serves during the deploy (section 13).
            migrationBuilder.CreateTable(
                name: "tenant_purge_sagas",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    waiting = table.Column<List<string>>(type: "text[]", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_purge_sagas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_purge_sagas_tenant_id",
                schema: "control_plane",
                table: "tenant_purge_sagas",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_purge_sagas",
                schema: "control_plane");
        }
    }
}
