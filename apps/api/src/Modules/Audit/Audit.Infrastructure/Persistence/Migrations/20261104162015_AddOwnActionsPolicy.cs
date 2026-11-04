using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnActionsPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R11: a person's data export reads the person's own actions in every tenant's log. A transaction declares one tenant at most
            // (R4), so it declares the user instead, and this policy shows it the entries whose actor is that user, and no other entry.
            // FOR SELECT only: a write policy would widen access (R11).
            migrationBuilder.Sql("""
                CREATE POLICY own_actions ON audit.entries FOR SELECT TO api_application
                    USING (actor_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY own_actions ON audit.entries;");
        }
    }
}
