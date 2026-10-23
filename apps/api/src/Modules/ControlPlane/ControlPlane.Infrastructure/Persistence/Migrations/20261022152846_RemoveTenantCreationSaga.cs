using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTenantCreationSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A tenant is created active in one transaction, so the statuses of the creation saga go (section 6, "Tenant creation
            // process"). A provisioning tenant has its Owner invitation and becomes active: the platform invites its Owner again
            // (InviteOwner). A cancelled tenant becomes closed at the migration's time, so it can be reopened within the reopen period;
            // its cancellation already cancelled its invitation. The tenants table belongs to no tenant (R6).
            migrationBuilder.Sql("""
                UPDATE control_plane.tenants SET status = 'Active' WHERE status = 'Provisioning';
                UPDATE control_plane.tenants SET status = 'Closed', closed_at = now() WHERE status = 'Cancelled';
                """);

            migrationBuilder.DropTable(
                name: "tenant_creation_sagas",
                schema: "control_plane");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                schema: "control_plane",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                schema: "control_plane",
                table: "tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_creation_sagas",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancellation_timeout = table.Column<TimeSpan>(type: "interval", nullable: false),
                    invitation_email_timeout = table.Column<TimeSpan>(type: "interval", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_creation_sagas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_creation_sagas_tenant_id",
                schema: "control_plane",
                table: "tenant_creation_sagas",
                column: "tenant_id");
        }
    }
}
