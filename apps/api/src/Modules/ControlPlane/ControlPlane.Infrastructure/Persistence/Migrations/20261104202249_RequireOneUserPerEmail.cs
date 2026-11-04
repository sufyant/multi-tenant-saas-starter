using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireOneUserPerEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A7: two live users never keep one address. The index fails to build on a database where two live users keep one; the
            // maintainer finds them first with the query of the pull request of step 36's second part, and fixes them.
            migrationBuilder.DropIndex(
                name: "ix_users_email",
                schema: "control_plane",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                schema: "control_plane",
                table: "users",
                column: "email",
                unique: true,
                filter: "email IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_users_email",
                schema: "control_plane",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "control_plane",
                table: "users",
                column: "email");
        }
    }
}
