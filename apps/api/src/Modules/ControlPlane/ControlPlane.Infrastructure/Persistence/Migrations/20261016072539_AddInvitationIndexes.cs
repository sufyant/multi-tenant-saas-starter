using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_invitations_email",
                schema: "control_plane",
                table: "invitations",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_email_trgm",
                schema: "control_plane",
                table: "invitations",
                column: "email")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_invitations_expires_at",
                schema: "control_plane",
                table: "invitations",
                column: "expires_at");

            // K2, K6: one pending invitation for each tenant and address, compared without case. EF Core models no index on an
            // expression, so the model does not hold it; ControlPlaneDbContext names it.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_invitations_pending_email ON control_plane.invitations (tenant_id, lower(email)) WHERE status = 'Pending';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX control_plane.ix_invitations_pending_email;");

            migrationBuilder.DropIndex(
                name: "ix_invitations_email",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.DropIndex(
                name: "ix_invitations_email_trgm",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.DropIndex(
                name: "ix_invitations_expires_at",
                schema: "control_plane",
                table: "invitations");
        }
    }
}
