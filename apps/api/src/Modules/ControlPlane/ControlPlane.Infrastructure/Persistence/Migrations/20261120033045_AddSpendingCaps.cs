using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpendingCaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spending_caps",
                schema: "control_plane",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    reached_period = table.Column<DateOnly>(type: "date", nullable: true),
                    reached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spending_caps", x => x.tenant_id);
                    table.CheckConstraint("ck_spending_caps_amount", "amount BETWEEN 1 AND 10000000000");
                    table.CheckConstraint("ck_spending_caps_reached", "(reached_period IS NULL) = (reached_at IS NULL) AND (reached_period IS NULL OR extract(day FROM reached_period) = 1)");
                    table.ForeignKey(
                        name: "fk_spending_caps_tenants_tenant_id",
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
                name: "spending_caps",
                schema: "control_plane");
        }
    }
}
