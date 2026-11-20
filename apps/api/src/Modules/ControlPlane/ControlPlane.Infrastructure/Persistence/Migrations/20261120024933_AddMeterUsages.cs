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
                    extra = table.Column<long>(type: "bigint", nullable: false),
                    unit_price = table.Column<long>(type: "bigint", nullable: false),
                    unit_size = table.Column<long>(type: "bigint", nullable: false),
                    quota_reached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meter_usages", x => new { x.tenant_id, x.meter_key, x.period });
                    table.CheckConstraint("ck_meter_usages_counts", "used >= 0 AND extra BETWEEN 0 AND used AND unit_price BETWEEN 0 AND 10000000000 AND unit_size BETWEEN 1 AND 1000000000000");
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
