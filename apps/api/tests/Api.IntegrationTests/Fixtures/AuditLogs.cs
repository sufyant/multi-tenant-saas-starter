namespace Api.IntegrationTests.Fixtures;

// What the two audit logs hold about one target, read as the superuser (K3). Each record reads
// "operation|actor kind|actor id|target kind|target id|target name", with "-" for an empty value, oldest first and joined by ", ".
// A record of the tenant's log starts with its tenant id.
internal sealed class AuditLogs(Database database)
{
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

    // How many records of the tenant's log hold the text anywhere in their row, details included.
    public Task<long> TenantRecordsHoldingAsync(Guid tenantId, string text) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.entries AS entry WHERE entry.tenant_id = '{tenantId}' AND entry::text ILIKE '%{text}%'");

    private const string Record =
        "concat_ws('|', operation, actor_kind, coalesce(actor_id::text, '-'), target_kind, target_id, coalesce(target_name, '-'))";
}
