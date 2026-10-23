using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KeepATombstoneOfAPurgedTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // K4: a purged tenant's row is its tombstone, with the moment of its purge. The column only adds, and the version before it
            // writes no purged tenant, so it still serves during the deploy (section 13).
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "purged_at",
                schema: "control_plane",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "purged_at",
                schema: "control_plane",
                table: "tenants");
        }
    }
}
