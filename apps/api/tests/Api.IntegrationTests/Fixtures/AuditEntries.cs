namespace Api.IntegrationTests.Fixtures;

// What the two audit logs hold about one target, read as the superuser (K3). Each record reads
// "operation|actor kind|actor id|target kind|target id|target name", with "-" for an empty value, oldest first and joined by ", ".
// A record of the tenant's log starts with its tenant id. A test that reads the logs through the API adds its records here.
internal sealed class AuditEntries(Database database)
{
    private const string Record =
        "concat_ws('|', operation, actor_kind, coalesce(actor_id::text, '-'), target_kind, target_id, coalesce(target_name, '-'))";

    public Task<string?> TenantLogAsync(Guid targetId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(tenant_id || ':' || {Record}, ', ' ORDER BY occurred_at, id) FROM audit.entries WHERE target_id = '{targetId}'");

    public Task<string?> PlatformLogAsync(Guid targetId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg({Record}, ', ' ORDER BY occurred_at, id) FROM audit.platform_entries WHERE target_id = '{targetId}'");

    // The details of the tenant's records about the target, oldest first and joined by ", ".
    public Task<string?> TenantDetailsAsync(Guid targetId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(coalesce(details::text, '-'), ', ' ORDER BY occurred_at, id) FROM audit.entries WHERE target_id = '{targetId}'");

    // The details of the platform's entries about the target, oldest first and joined by ", ".
    public Task<string?> PlatformDetailsAsync(Guid targetId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(coalesce(details::text, '-'), ', ' ORDER BY occurred_at, id) FROM audit.platform_entries WHERE target_id = '{targetId}'");

    // How many records of the tenant's log hold the text anywhere in their row, details included.
    public Task<long> TenantRecordsHoldingAsync(Guid tenantId, string text) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.entries AS entry WHERE entry.tenant_id = '{tenantId}' AND entry::text ILIKE '%{text}%'");

    // Adds a record to a tenant's log as the superuser, whom row level security does not bind, so the tenant column is written by hand.
    // Without a target, the record is about the tenant.
    public async Task<Guid> AddTenantEntryAsync(
        Guid tenantId,
        DateTimeOffset occurredAt,
        string actorKind = "Platform",
        Guid? actorId = null,
        string operation = "tenant.suspended",
        string targetKind = "Tenant",
        Guid? targetId = null,
        string? targetName = null,
        string? details = null)
    {
        var id = Guid.CreateVersion7();
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id, target_name, details)
            VALUES ('{id}', '{tenantId}', '{occurredAt:O}', '{actorKind}', {Text(actorId)}, 'Command', '{operation}', '{targetKind}',
                '{targetId ?? tenantId}', {Text(targetName)}, {Text(details)})
            """);
        return id;
    }

    public async Task<Guid> AddPlatformEntryAsync(
        DateTimeOffset occurredAt,
        string actorKind,
        Guid? actorId,
        string targetKind,
        Guid targetId,
        string operation = "user.suspended",
        string? targetName = null)
    {
        var id = Guid.CreateVersion7();
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.platform_entries (id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id, target_name)
            VALUES ('{id}', '{occurredAt:O}', '{actorKind}', {Text(actorId)}, 'Command', '{operation}', '{targetKind}', '{targetId}', {Text(targetName)})
            """);
        return id;
    }

    private static string Text(object? value) => value is null ? "NULL" : $"'{value}'";
}
