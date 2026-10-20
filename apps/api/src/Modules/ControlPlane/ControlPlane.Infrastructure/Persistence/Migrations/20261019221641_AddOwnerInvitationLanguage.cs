using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerInvitationLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "owner_invitation_language",
                schema: "control_plane",
                table: "tenants",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);

            // R12: the language of the latest Owner invitation of each tenant that has no Owner, from the tenant's own invitations. Row level
            // security holds the migration account too, so the statement declares each tenant in turn (R4).
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    tenant uuid;
                    owner uuid := '00000000-0000-7000-8000-000000000001';
                BEGIN
                    FOR tenant IN SELECT id FROM control_plane.tenants WHERE owner_invitation_email IS NOT NULL LOOP
                        PERFORM set_config('app.tenant_id', tenant::text, true);
                        UPDATE control_plane.tenants SET owner_invitation_language = (
                            SELECT language FROM control_plane.invitations
                            WHERE role_id = owner AND NOT EXISTS (SELECT FROM control_plane.memberships WHERE role_id = owner)
                            ORDER BY created_at DESC, id DESC LIMIT 1)
                        WHERE id = tenant;
                    END LOOP;
                    PERFORM set_config('app.tenant_id', '', true);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "owner_invitation_language",
                schema: "control_plane",
                table: "tenants");
        }
    }
}
