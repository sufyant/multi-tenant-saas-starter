namespace Audit.IntegrationTests.Fixtures;

// Reads one record of an audit log as the application role, the way a reader of that log would: the tenant's log in that tenant's
// transaction, the platform's log in a transaction that declares nothing (R11).
internal static class Logs
{
    public const string Tenant = "audit.entries";

    public const string Platform = "audit.platform_entries";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // "operation|actor kind|actor id|target kind|target id|target name", with "-" for an empty value; null when there is no record.
    public static Task<string?> DescribeAsync(IServiceProvider services, string log, Guid? tenantId, Guid entryId) =>
        InTenant.SqlAsync(
            services,
            tenantId,
            async command => await command.ExecuteScalarAsync(Cancellation) as string,
            $"""
            SELECT concat_ws('|', operation, actor_kind, coalesce(actor_id, '-'), target_kind, target_id, coalesce(target_name, '-'))
            FROM {log} WHERE id = '{entryId}'
            """);

    public static Task<long> CountAsync(IServiceProvider services, string log, Guid? tenantId, Guid entryId) =>
        InTenant.SqlAsync(
            services, tenantId, async command => (long)(await command.ExecuteScalarAsync(Cancellation))!, $"SELECT count(*) FROM {log} WHERE id = '{entryId}'");
}
