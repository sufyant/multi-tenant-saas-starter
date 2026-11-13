using Audit.Domain.Retention;

namespace Audit.Application.AuditLogs;

/// <summary>
/// The retention of the audit logs (K14, K15): the active tenant's row of the retention, locked, and the two functions that delete a batch of
/// expired entries (R19). No other code deletes an entry, apart from the tenant purge (R14).
/// </summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IRetentionStore
{
    /// <summary>
    /// The active tenant's row, written first when it is missing, with no limit, and then locked for the rest of the transaction, so two
    /// writers run one after the other (K6o).
    /// </summary>
    internal Task<TenantRetention> LockTenantAsync(CancellationToken cancellationToken);

    /// <summary>Whether the active tenant's log holds an entry.</summary>
    internal Task<bool> HasEntriesAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the active tenant's locked row: a tenant without an entry keeps none (K14).</summary>
    internal Task RemoveTenantAsync(TenantRetention row, CancellationToken cancellationToken);

    /// <summary>Saves the row's changes in the handler's transaction.</summary>
    internal Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>Deletes up to the batch of the active tenant's entries older than the moment; returns how many went.</summary>
    internal Task<int> DeleteExpiredEntriesAsync(DateTimeOffset before, int batch, CancellationToken cancellationToken);

    /// <summary>Deletes up to the batch of the platform's entries older than the moment, in a transaction that declares nobody.</summary>
    internal Task<int> DeleteExpiredPlatformEntriesAsync(DateTimeOffset before, int batch, CancellationToken cancellationToken);
}
