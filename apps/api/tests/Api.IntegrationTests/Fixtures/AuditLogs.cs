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

    private const string Record =
        "concat_ws('|', operation, actor_kind, coalesce(actor_id::text, '-'), target_kind, target_id, coalesce(target_name, '-'))";
}
