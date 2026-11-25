using SharedKernel;

namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A tenant's effective entitlements, for the host's feature check on a tenant route and for another module that gates its own work
/// (docs/flows/plans-and-entitlements.md). Each answer is computed at the moment of the call, so an override stops at its expiry.
/// </summary>
public interface IEntitlementDirectory
{
    /// <summary>The effective value of every key of the list for the tenant with this id, which exists.</summary>
    Task<TenantEntitlements> FindAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// The effective number of the limit for the tenant with this id, which exists, or null for unlimited. It reads the database, not the
    /// cache, so a module that checks its limit under its own lock sees a plan change that just committed (E6, X3).
    /// </summary>
    Task<int?> ReadLimitAsync(Guid tenantId, string limit, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the feature is on for the tenant with this id; when it is off, 403 <c>plan.feature_unavailable</c>, with the key in
    /// <see cref="Error.Entitlement"/>.
    /// </summary>
    Task<Result> CheckFeatureAsync(Guid tenantId, string feature, CancellationToken cancellationToken);
}
