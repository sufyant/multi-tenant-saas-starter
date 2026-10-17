using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "first_owner_id",
                schema: "control_plane",
                table: "tenants",
                type: "uuid",
                nullable: true);

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
                name: "owner_invitation_status",
                schema: "control_plane",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "joined_at",
                schema: "control_plane",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_first_owner_id",
                schema: "control_plane",
                table: "tenants",
                column: "first_owner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_users_first_owner_id",
                schema: "control_plane",
                table: "tenants",
                column: "first_owner_id",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // R12: the summary of the tenants that exist, from their own rows. Row level security holds the migration account too, so the
            // statement declares each tenant in turn (section 4: work across tenants declares each tenant in turn). A member joined when
            // they last accepted an invitation of the tenant; a membership without one keeps the moment of this migration.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    tenant uuid;
                    owner uuid := '00000000-0000-7000-8000-000000000001';
                BEGIN
                    FOR tenant IN SELECT id FROM control_plane.tenants LOOP
                        PERFORM set_config('app.tenant_id', tenant::text, true);
                        UPDATE control_plane.memberships AS membership SET joined_at = accepted.at
                        FROM (SELECT accepted_by, max(accepted_at) AS at FROM control_plane.invitations WHERE accepted_by IS NOT NULL GROUP BY accepted_by) AS accepted
                        WHERE accepted.accepted_by = membership.user_id;
                        WITH latest AS (
                            SELECT email, expires_at, status FROM control_plane.invitations
                            WHERE role_id = owner AND NOT EXISTS (SELECT FROM control_plane.memberships WHERE role_id = owner)
                            ORDER BY created_at DESC, id DESC LIMIT 1)
                        UPDATE control_plane.tenants SET
                            member_count = (SELECT count(*) FROM control_plane.memberships),
                            owner_count = (SELECT count(*) FROM control_plane.memberships WHERE role_id = owner),
                            first_owner_id = (SELECT user_id FROM control_plane.memberships WHERE role_id = owner ORDER BY joined_at, user_id LIMIT 1),
                            owner_invitation_email = (SELECT email FROM latest),
                            owner_invitation_expires_at = (SELECT expires_at FROM latest),
                            owner_invitation_status = (SELECT status FROM latest)
                        WHERE id = tenant;
                    END LOOP;
                    PERFORM set_config('app.tenant_id', '', true);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tenants_users_first_owner_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_first_owner_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "first_owner_id",
                schema: "control_plane",
                table: "tenants");

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
                name: "owner_invitation_status",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "joined_at",
                schema: "control_plane",
                table: "memberships");
        }
    }
}
