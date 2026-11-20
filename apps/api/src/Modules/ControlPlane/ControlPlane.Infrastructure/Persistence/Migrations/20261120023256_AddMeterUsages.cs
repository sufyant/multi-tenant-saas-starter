using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeterUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meter_usages",
                schema: "control_plane",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    meter_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    period = table.Column<DateOnly>(type: "date", nullable: false),
                    used = table.Column<long>(type: "bigint", nullable: false),
                    extra = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meter_usages", x => new { x.tenant_id, x.meter_key, x.period });
                    table.CheckConstraint("ck_meter_usages_counts", "used >= 0 AND extra BETWEEN 0 AND used");
                    table.CheckConstraint("ck_meter_usages_period", "extract(day FROM period) = 1");
                    table.ForeignKey(
                        name: "fk_meter_usages_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_usages",
                schema: "control_plane");
        }
    }
}
