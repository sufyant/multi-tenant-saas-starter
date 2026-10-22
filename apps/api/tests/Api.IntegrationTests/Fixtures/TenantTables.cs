namespace Api.IntegrationTests.Fixtures;

// R9: every tenant table of the database, with the SQL that writes one row of it for a tenant. The rows a table refers to are
// written in the same statement, so a failed write leaves nothing behind. A new tenant table is one more line here;
// TenantTableIsolationTests fails until it has one.
internal static class TenantTables
{
    // The Member role the declared tenant sees. Without a tenant, row level security hides every role, so the row names none and the
    // insert is refused by the policy all the same.
    private const string MemberRole = "(SELECT id FROM control_plane.roles WHERE built_in = 'Member')";

    private static readonly Dictionary<string, TenantTable> All = new(StringComparer.Ordinal)
    {
        ["audit.entries"] = new(
            ApplicationMayUpdate: false,
            tenant => $"""
                INSERT INTO audit.entries (id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id, tenant_id)
                VALUES (gen_random_uuid(), now(), 'User', gen_random_uuid(), 'Command', 'Isolation', 'Tenant', '{tenant}', '{tenant}')
                """),
        ["control_plane.invitations"] = new(ApplicationMayUpdate: true, tenant => InvitationRow(tenant, MemberRole)),
        ["control_plane.memberships"] = new(ApplicationMayUpdate: true, tenant => MembershipRow(tenant, MemberRole)),
        ["control_plane.role_permissions"] = new(
            ApplicationMayUpdate: true,
            tenant => $"""
                WITH role AS (
                    INSERT INTO control_plane.roles (id, tenant_id, name) VALUES (gen_random_uuid(), '{tenant}', 'Isolation granted') RETURNING id)
                INSERT INTO control_plane.role_permissions (tenant_id, role_id, permission) SELECT '{tenant}', role.id, 'tenant.members.read' FROM role
                """),
        ["control_plane.roles"] = new(
            ApplicationMayUpdate: true,
            tenant => $"INSERT INTO control_plane.roles (id, tenant_id, name) VALUES (gen_random_uuid(), '{tenant}', 'Isolation')",
            OwnRows: "name = 'Isolation'"),
        ["probes.probes"] = new(
            ApplicationMayUpdate: true,
            tenant => $"INSERT INTO probes.probes (id, value, tenant_id) VALUES (gen_random_uuid(), 'isolation', '{tenant}')"),
    };

    public static IEnumerable<string> Names => All.Keys;

    // Audit entries are never changed once written, so the application role has no update privilege on them at all.
    public static IEnumerable<string> Updatable => All.Where(table => table.Value.ApplicationMayUpdate).Select(table => table.Key);

    public static string InsertRow(string table, Guid tenantId) => All[table].InsertRow(tenantId);

    // How many of the tenant's rows of the table a test wrote. A tenant has rows of some tables from its creation on, such as its roles
    // (A2), so those tables name the rows the insert writes.
    public static string CountRowsOf(string table, Guid tenantId) =>
        $"SELECT count(*) FROM {table} WHERE tenant_id = '{tenantId}' AND {All[table].OwnRows}";

    // A2, A3: a row of a table that names a role, with the role given by its id.
    public static string InsertRowWithRole(string table, Guid tenantId, Guid roleId) => table switch
    {
        "control_plane.invitations" => InvitationRow(tenantId, $"'{roleId}'"),
        "control_plane.memberships" => MembershipRow(tenantId, $"'{roleId}'"),
        "control_plane.role_permissions" =>
            $"INSERT INTO control_plane.role_permissions (tenant_id, role_id, permission) VALUES ('{tenantId}', '{roleId}', 'tenant.members.read')",
        _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Only memberships, invitations and role permissions name a role."),
    };

    private static string InvitationRow(Guid tenant, string role) =>
        $"""
        WITH inviter AS (
            INSERT INTO control_plane.users (id, external_id, language) VALUES (gen_random_uuid(), 'user_' || gen_random_uuid(), 'en') RETURNING id)
        INSERT INTO control_plane.invitations (id, tenant_id, email, role_id, invited_by, created_at, expires_at, status, language)
        SELECT gen_random_uuid(), '{tenant}', 'invited@example.com', {role}, inviter.id, now(), now() + interval '7 days', 'Pending', 'en'
        FROM inviter
        """;

    private static string MembershipRow(Guid tenant, string role) =>
        $"""
        WITH member AS (
            INSERT INTO control_plane.users (id, external_id, language) VALUES (gen_random_uuid(), 'user_' || gen_random_uuid(), 'en') RETURNING id)
        INSERT INTO control_plane.memberships (tenant_id, user_id, role_id, joined_at)
        SELECT '{tenant}', member.id, {role}, now() FROM member
        """;

    private sealed record TenantTable(bool ApplicationMayUpdate, Func<Guid, string> InsertRow, string OwnRows = "true");
}
