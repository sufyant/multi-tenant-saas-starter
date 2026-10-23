using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LetThePurgeDeleteATenantsLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R14, K3: the application cannot change an audit entry or delete one alone; it holds SELECT and INSERT only. The tenant purge
            // deletes a tenant's whole log through this function, which runs as its owner, the migration role, and deletes only the entries
            // of the tenant the transaction declares (R4). Row level security, forced, still binds the owner, so the policy and the WHERE
            // clause both keep the deletion to that tenant. The search path is fixed, so no object of another schema stands in for a name
            // (PostgreSQL documentation, CREATE FUNCTION, "Writing SECURITY DEFINER Functions Safely"). Only the application may execute it.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit.purge_tenant_entries() RETURNS integer
                    LANGUAGE plpgsql
                    SECURITY DEFINER
                    SET search_path = pg_catalog, pg_temp
                AS $$
                DECLARE
                    tenant uuid := NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                    deleted integer;
                BEGIN
                    IF tenant IS NULL THEN
                        RAISE EXCEPTION 'audit.purge_tenant_entries() deletes the entries of the declared tenant, and no tenant is declared';
                    END IF;

                    DELETE FROM audit.entries WHERE tenant_id = tenant;
                    GET DIAGNOSTICS deleted = ROW_COUNT;
                    RETURN deleted;
                END
                $$;
                REVOKE ALL ON FUNCTION audit.purge_tenant_entries() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION audit.purge_tenant_entries() TO api_application;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION audit.purge_tenant_entries();");
        }
    }
}
