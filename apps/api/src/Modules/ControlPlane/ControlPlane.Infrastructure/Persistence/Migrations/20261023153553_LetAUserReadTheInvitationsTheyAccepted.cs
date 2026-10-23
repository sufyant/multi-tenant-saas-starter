using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LetAUserReadTheInvitationsTheyAccepted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R11: a read that declares only the user sees the invitations the user accepted, in every tenant, so a user's deletion finds
            // the tenants the user joined, also where no membership is left (A8). It is FOR SELECT only: permissive policies combine with
            // OR, so a policy that allowed a write here would let a user change a tenant's invitation. It only adds, so the version before
            // it still serves during the deploy.
            migrationBuilder.Sql(
                "CREATE POLICY own_accepted_invitations ON control_plane.invitations FOR SELECT "
                + "USING (accepted_by = NULLIF(current_setting('app.user_id', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY own_accepted_invitations ON control_plane.invitations;");
        }
    }
}
