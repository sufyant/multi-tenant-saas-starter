using ControlPlane.Domain.FeatureFlags;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.FeatureFlags;

/// <summary>
/// The feature flags, their listed organisations and their history, for the commands. They belong to no tenant (R6), so a command reaches
/// them in a transaction that declares none.
/// </summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IFeatureFlagRepository
{
    /// <summary>
    /// Locks the flags against another deploy's sync for the rest of the transaction, so two processes that start at once store a new flag
    /// once. It locks no row, so a rule's change never waits for it.
    /// </summary>
    internal Task LockForSyncAsync(CancellationToken cancellationToken);

    /// <summary>Every stored flag, each locked for the rest of the transaction in the order of its id, for the sync, which runs under <see cref="LockForSyncAsync"/>.</summary>
    internal Task<IReadOnlyList<FeatureFlag>> ListFlagsForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>The flag with this key, locked for the rest of the transaction, so its rules change one after the other (K6a).</summary>
    internal Task<FeatureFlag?> FindFlagForUpdateAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// The organisation's status and name, with its row locked against a change but not against another reader, so its purge and its
    /// closure wait for a listing, and then find it (K6a). A command that lists or removes an organisation takes it before the flag, as the
    /// purge takes the tenant's row before the flags (K6c). None for an organisation that does not exist.
    /// </summary>
    internal Task<(TenantStatus Status, string Name)?> FindTenantForShareAsync(Guid tenantId, CancellationToken cancellationToken);

    internal Task<bool> IsListedAsync(Guid flagId, Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The organisations that the flag's first rule lists.</summary>
    internal Task<IReadOnlySet<Guid>> ListListedAsync(Guid flagId, CancellationToken cancellationToken);

    /// <summary>Every organisation a flag can reach: neither purging nor purged.</summary>
    internal Task<IReadOnlyList<Guid>> ListReachableTenantIdsAsync(CancellationToken cancellationToken);

    internal void Add(FeatureFlag flag);

    internal void List(FeatureFlagTenant listed);

    internal Task UnlistAsync(Guid flagId, Guid tenantId, CancellationToken cancellationToken);

    internal void Record(FeatureFlagChange change);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
