using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveTenantSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_summaries",
                schema: "control_plane",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    owner_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    owner_invitation_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    owner_invitation_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    owner_invitation_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    owner_invitation_language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_summaries", x => x.tenant_id);
                    table.ForeignKey(
                        name: "fk_tenant_summaries_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // R12: every tenant's summary moves to its own row before the old columns go, so no summary is lost.
            migrationBuilder.Sql("""
                INSERT INTO control_plane.tenant_summaries (tenant_id, member_count, owner_count, owner_invitation_email,
                    owner_invitation_expires_at, owner_invitation_status, owner_invitation_language)
                SELECT id, member_count, owner_count, owner_invitation_email, owner_invitation_expires_at, owner_invitation_status,
                    owner_invitation_language
                FROM control_plane.tenants;
                """);

            migrationBuilder.DropColumn(
                name: "member_count",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_count",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_invitation_email",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_invitation_expires_at",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_invitation_language",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_invitation_status",
                schema: "control_plane",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "member_count",
                schema: "control_plane",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "owner_count",
                schema: "control_plane",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "owner_invitation_email",
                schema: "control_plane",
                table: "tenants",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "owner_invitation_expires_at",
                schema: "control_plane",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_invitation_language",
                schema: "control_plane",
                table: "tenants",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_invitation_status",
                schema: "control_plane",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE control_plane.tenants AS tenant SET member_count = summary.member_count, owner_count = summary.owner_count,
                    owner_invitation_email = summary.owner_invitation_email, owner_invitation_expires_at = summary.owner_invitation_expires_at,
                    owner_invitation_status = summary.owner_invitation_status, owner_invitation_language = summary.owner_invitation_language
                FROM control_plane.tenant_summaries AS summary WHERE summary.tenant_id = tenant.id;
                """);

            migrationBuilder.DropTable(
                name: "tenant_summaries",
                schema: "control_plane");
        }
    }
}
