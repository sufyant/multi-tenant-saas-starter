namespace ControlPlane.Contracts.Plans;

/// <summary>
/// The tenants' Activity log periods, for the daily job of the audit retention (K14). Each answer is computed at the moment of the call, so
/// an override stops at its expiry. A purging or a purged tenant has none: its log goes with its purge (R14).
/// </summary>
public interface IAuditRetentionDirectory
{
    /// <summary>The period of every tenant that is not purging or purged, in one query across the tenants (R6, R12).</summary>
    Task<IReadOnlyList<TenantAuditRetention>> ListAsync(CancellationToken cancellationToken);

    /// <summary>The period of the tenant with this id; none when it does not exist, or is purging or purged.</summary>
    Task<TenantAuditRetention?> FindAsync(Guid tenantId, CancellationToken cancellationToken);
}
