namespace Api.IntegrationTests.Fixtures;

// R6: the explicit list of the tables that belong to no tenant. Every other table of the application holds tenant rows and must
// be isolated; DatabaseTables checks every table of the database against this list.
internal static class TablesWithoutTenant
{
    // Every module's migration history, which sits in the module's own schema.
    public const string MigrationHistory = "__ef_migrations_history";

    // ControlPlane's data above tenants (the roles belong to a tenant, A2), the tenants' summaries and entitlement overrides, which the
    // platform reads across tenants as it reads the tenants (R12), the plans, their values and the default mark, the platform's audit log,
    // and the host's idempotency keys.
    public static readonly IReadOnlySet<string> Tables = new HashSet<string>(StringComparer.Ordinal)
    {
        "control_plane.tenants",
        "control_plane.tenant_summaries",
        "control_plane.entitlement_overrides",
        "control_plane.plans",
        "control_plane.plan_entitlements",
        "control_plane.default_plans",
        "control_plane.users",
        "control_plane.system_admins",
        "api.idempotency_keys",
        "audit.platform_entries",
    };

    // Every table in these schemas: Wolverine's message storage.
    public static readonly IReadOnlySet<string> Schemas = new HashSet<string>(StringComparer.Ordinal) { "wolverine" };

    public static bool Contains(string schema, string table) =>
        Tables.Contains($"{schema}.{table}") || Schemas.Contains(schema) || table == MigrationHistory;
}
