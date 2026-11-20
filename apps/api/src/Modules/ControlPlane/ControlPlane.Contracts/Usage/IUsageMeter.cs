namespace ControlPlane.Contracts.Usage;

/// <summary>
/// The one count of a tenant's use of a meter, which a product's module asks before the use (step 40c,
/// docs/flows/plans-and-entitlements.md). The count is the tenant's for each meter and calendar month of UTC. Each call runs in a short
/// transaction of its own, under the lock of the month's row, so a stop meter stops exactly at its quota.
/// </summary>
public interface IUsageMeter
{
    /// <summary>Whether the tenant with this id may use the meter by this quantity, at least 1; when it may, the use is counted.</summary>
    Task<MeterUse> UseAsync(Guid tenantId, string meterKey, long quantity, CancellationToken cancellationToken);
}
