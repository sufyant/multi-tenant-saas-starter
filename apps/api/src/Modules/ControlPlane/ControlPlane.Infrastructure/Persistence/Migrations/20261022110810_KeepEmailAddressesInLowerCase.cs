using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KeepEmailAddressesInLowerCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every stored address takes the one form the API now writes (EmailAddress): trimmed, which every writer did already, and in
            // lower case by .NET's invariant culture. PostgreSQL's lower() gives that form but for one letter: it makes the Turkish dotted
            // capital I (U+0130) an "i", and .NET keeps it. So the letter is held out of lower(), behind chr(1), which no address holds.
            // Row level security holds the migration account too, so the invitations are lowercased with each tenant declared in turn
            // (R4). The users and the tenants' summaries belong to no tenant (R6).
            migrationBuilder.Sql("""
                CREATE FUNCTION pg_temp.invariant_lower(address text) RETURNS text LANGUAGE sql IMMUTABLE
                    RETURN replace(lower(replace(address, chr(304), chr(1))), chr(1), chr(304));
                UPDATE control_plane.users SET email = pg_temp.invariant_lower(email) WHERE email <> pg_temp.invariant_lower(email);
                UPDATE control_plane.tenant_summaries SET owner_invitation_email = pg_temp.invariant_lower(owner_invitation_email)
                WHERE owner_invitation_email <> pg_temp.invariant_lower(owner_invitation_email);
                DO $$
                DECLARE
                    tenant uuid;
                BEGIN
                    FOR tenant IN SELECT id FROM control_plane.tenants LOOP
                        PERFORM set_config('app.tenant_id', tenant::text, true);
                        UPDATE control_plane.invitations SET email = pg_temp.invariant_lower(email) WHERE email <> pg_temp.invariant_lower(email);
                    END LOOP;
                    PERFORM set_config('app.tenant_id', '', true);
                END $$;
                DROP FUNCTION pg_temp.invariant_lower(text);
                """);

            // K2: the unique index of pending invitations moves from lower(email) to the stored value. No two pending invitations of one
            // tenant can share the new form: the old index refused two that shared lower(), and the new form tells apart at least as
            // much as lower() does.
            migrationBuilder.Sql("DROP INDEX control_plane.ix_invitations_pending_email;");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_pending_email",
                schema: "control_plane",
                table: "invitations",
                columns: new[] { "tenant_id", "email" },
                unique: true,
                filter: "status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_invitations_pending_email",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_invitations_pending_email ON control_plane.invitations (tenant_id, lower(email)) WHERE status = 'Pending';");
        }
    }
}
