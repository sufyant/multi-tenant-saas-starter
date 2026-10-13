namespace Api.IntegrationTests;

// R6: the explicit list of the tables that belong to no tenant. Every other table of the application holds tenant rows and must
// be isolated; DatabaseTables checks every table of the database against this list.
internal static class TablesWithoutTenant
{
    // ControlPlane's data above tenants.
    public static readonly IReadOnlySet<string> Tables = new HashSet<string>(StringComparer.Ordinal)
    {
        "control_plane.tenants",
        "control_plane.users",
        "control_plane.roles",
        "control_plane.system_admins",
        "control_plane.tenant_creation_requests",
    };

    // Every table in these schemas: Wolverine's message storage.
    public static readonly IReadOnlySet<string> Schemas = new HashSet<string>(StringComparer.Ordinal) { "wolverine" };

    // Every module's migration history, which sits in the module's own schema.
    public const string MigrationHistory = "__ef_migrations_history";

    public static bool Contains(string schema, string table) =>
        Tables.Contains($"{schema}.{table}") || Schemas.Contains(schema) || table == MigrationHistory;
}
