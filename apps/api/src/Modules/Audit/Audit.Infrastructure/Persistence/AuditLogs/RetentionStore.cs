using Audit.Application.AuditLogs;
using Audit.Domain.Retention;
using Microsoft.EntityFrameworkCore;

namespace Audit.Infrastructure.Persistence.AuditLogs;

/// <summary>The retention of the audit logs on the module's DbContext, in the transaction Wolverine began for the handler.</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class RetentionStore(AuditDbContext audit) : IRetentionStore
{
    // K6o: the row is written when it is missing, with no limit, and the tenant column takes the declared tenant. Then it is locked, so
    // a second writer waits for this transaction and reads what it wrote. Row level security keeps both statements to the tenant. A writer
    // that waited for the purge's lock finds the row deleted: it writes the row again, and that second lock cannot wait for a deletion.
    async Task<TenantRetention> IRetentionStore.LockTenantAsync(CancellationToken cancellationToken)
    {
        _ = audit.MessageTenantId ?? throw new InvalidOperationException("A tenant's retention is read inside the tenant.");
        return await LockAsync(cancellationToken) ?? await LockAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant's row of the retention was written and locked in this transaction.");
    }

    async Task<TenantRetention> IRetentionStore.FindTenantAsync(CancellationToken cancellationToken) =>
        await audit.Retentions.AsNoTracking().SingleOrDefaultAsync(cancellationToken) ?? TenantRetention.Unwritten();

    Task<bool> IRetentionStore.HasEntriesOlderThanAsync(DateTimeOffset before, DateTimeOffset? from, CancellationToken cancellationToken) =>
        audit.Entries.AnyAsync(entry => entry.OccurredAt < before && (from == null || entry.OccurredAt >= from), cancellationToken);

    Task<bool> IRetentionStore.HasEntriesAsync(CancellationToken cancellationToken) => audit.Entries.AnyAsync(cancellationToken);

    Task IRetentionStore.RemoveTenantAsync(TenantRetention row, CancellationToken cancellationToken)
    {
        audit.Retentions.Remove(row);
        return audit.SaveChangesAsync(cancellationToken);
    }

    Task IRetentionStore.SaveAsync(CancellationToken cancellationToken) => audit.SaveChangesAsync(cancellationToken);

    // R19: the application may only execute the functions; each refuses a moment of the last 30 days.
    async Task<int> IRetentionStore.DeleteExpiredEntriesAsync(DateTimeOffset before, int batch, CancellationToken cancellationToken) =>
        await audit.Database.SqlQuery<int>($"SELECT audit.delete_expired_entries({before}, {batch}) AS \"Value\"").SingleAsync(cancellationToken);

    async Task<int> IRetentionStore.DeleteExpiredPlatformEntriesAsync(DateTimeOffset before, int batch, CancellationToken cancellationToken) =>
        await audit.Database.SqlQuery<int>($"SELECT audit.delete_expired_platform_entries({before}, {batch}) AS \"Value\"").SingleAsync(cancellationToken);

    private async Task<TenantRetention?> LockAsync(CancellationToken cancellationToken)
    {
        await audit.Database.ExecuteSqlAsync($"INSERT INTO audit.tenant_retentions (days) VALUES (NULL) ON CONFLICT (tenant_id) DO NOTHING", cancellationToken);
        return await audit.Retentions.FromSql($"SELECT * FROM audit.tenant_retentions FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    }
}
