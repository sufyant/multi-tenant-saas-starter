using Audit.Application.AuditLogs;
using Audit.Application.AuditLogs.DeleteExpiredEntries;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tenancy;

namespace Audit.IntegrationTests.AuditLogs;

// K14, K6o: the job's second step for a tenant locks the tenant's row of the retention, and keeps it only while the tenant still has an
// entry. The purge takes the same lock first, deletes the log and the row, and commits; a step that waited for it then finds no entry and
// keeps no row, at its first try.
public sealed class DeleteExpiredEntriesTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeleteExpiredEntries_WhileAPurgeHoldsTheRow_WaitsAndKeepsNoRow()
    {
        var tenantId = await Retentions.TenantWithRowAsync(database, days: 365);
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, kind, operation, target_kind, target_id)
            VALUES (gen_random_uuid(), '{tenantId}', now(), 'Platform', 'Command', 'tenant.suspended', 'Tenant', '{tenantId}')
            """);
        await using var purge = new NpgsqlConnection(database.ConnectionStringFor(DatabaseRoles.Application));
        await purge.OpenAsync(Cancellation);
        await using var purging = await purge.BeginTransactionAsync(Cancellation);
        await RunAsync(purge, purging, $"SELECT set_config('app.tenant_id', '{tenantId}', true)");
        await RunAsync(purge, purging, "INSERT INTO audit.tenant_retentions (days) VALUES (NULL) ON CONFLICT (tenant_id) DO NOTHING");
        await RunAsync(purge, purging, "SELECT 1 FROM audit.tenant_retentions FOR UPDATE");

        var step = InTenant.RunAsync(database.Services, tenantId, scope =>
            DeleteExpiredEntriesHandler.HandleAsync(
                new DeleteExpiredEntriesCommand(tenantId, 365, TimeProvider.System.GetUtcNow(), 0), scope.GetRequiredService<IRetentionStore>(), TimeProvider.System, Cancellation));
        await WaitingForALockAsync(purge.ProcessID);
        await RunAsync(purge, purging, "SELECT audit.purge_tenant_entries()");
        await RunAsync(purge, purging, "DELETE FROM audit.tenant_retentions");
        await purging.CommitAsync(Cancellation);
        await step;

        (await Retentions.RowAsync(database, tenantId)).ShouldBeNull();
    }

    private static async Task RunAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(Cancellation);
    }

    // The step waits for the purge's lock: one session waits for a lock that the purge's session holds. The other test classes run at
    // the same time on the same server, and their writers wait for their own locks, so only the sessions behind the purge count.
    private async Task WaitingForALockAsync(int purge)
    {
        var waiting = 0L;
        for (var tries = 0; tries < 100 && waiting == 0; tries++)
        {
            await Task.Delay(50, Cancellation);
            waiting = await database.ScalarAsSuperuserAsync<long>(
                $"SELECT count(*) FROM pg_stat_activity WHERE {purge} = ANY(pg_blocking_pids(pid))");
        }

        waiting.ShouldBe(1);
    }
}
