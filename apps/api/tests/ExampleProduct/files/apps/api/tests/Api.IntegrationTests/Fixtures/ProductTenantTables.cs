namespace Api.IntegrationTests.Fixtures;

// X8: the product's tenant tables, each with the SQL that writes one row of it for a tenant, as TenantTables holds the template's.
// The template ships this file empty and never changes it.
internal static class ProductTenantTables
{
    public static readonly Dictionary<string, TenantTables.TenantTable> All = new(StringComparer.Ordinal)
    {
        // The application role only adds and removes projects, so it may not update one.
        ["projects.projects"] = new(
            ApplicationMayUpdate: false,
            tenant => $"INSERT INTO projects.projects (id, tenant_id, name, created_by, created_at) VALUES (gen_random_uuid(), '{tenant}', 'Isolation', gen_random_uuid(), now())"),
    };
}
