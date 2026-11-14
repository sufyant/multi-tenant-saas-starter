using ControlPlane.Application.FeatureFlags;
using ControlPlane.Domain.FeatureFlags;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.FeatureFlags;

/// <summary>The feature flags for the commands. They belong to no tenant, so no row level security applies (R6).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class FeatureFlagRepository(ControlPlaneDbContext controlPlane) : IFeatureFlagRepository
{
    private const string SyncLock = "feature-flag-sync";

    async Task IFeatureFlagRepository.LockForSyncAsync(CancellationToken cancellationToken) =>
        await controlPlane.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({SyncLock}, 0))", cancellationToken);

    async Task<IReadOnlyList<FeatureFlag>> IFeatureFlagRepository.ListFlagsForUpdateAsync(CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync($"SELECT id FROM control_plane.feature_flags ORDER BY id FOR UPDATE", cancellationToken);
        return await controlPlane.FeatureFlags.ToListAsync(cancellationToken);
    }

    // The row is locked by its own statement, then read, so the lock never sits in a subquery that EF Core composes.
    async Task<FeatureFlag?> IFeatureFlagRepository.FindFlagForUpdateAsync(string key, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync($"SELECT id FROM control_plane.feature_flags WHERE key = {key} FOR UPDATE", cancellationToken);
        return await controlPlane.FeatureFlags.SingleOrDefaultAsync(flag => flag.Key == key, cancellationToken);
    }

    async Task<(TenantStatus Status, string Name)?> IFeatureFlagRepository.FindTenantForShareAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync($"SELECT id FROM control_plane.tenants WHERE id = {tenantId} FOR SHARE", cancellationToken);
        var tenant = await controlPlane.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Status, t.Name })
            .SingleOrDefaultAsync(cancellationToken);
        return tenant is null ? null : (tenant.Status, tenant.Name);
    }

    Task<bool> IFeatureFlagRepository.IsListedAsync(Guid flagId, Guid tenantId, CancellationToken cancellationToken) =>
        controlPlane.FeatureFlagTenants.AnyAsync(listed => listed.FlagId == flagId && listed.TenantId == tenantId, cancellationToken);

    async Task<IReadOnlySet<Guid>> IFeatureFlagRepository.ListListedAsync(Guid flagId, CancellationToken cancellationToken) =>
        (await controlPlane.FeatureFlagTenants.Where(listed => listed.FlagId == flagId).Select(listed => listed.TenantId).ToListAsync(cancellationToken)).ToHashSet();

    async Task<IReadOnlyList<Guid>> IFeatureFlagRepository.ListReachableTenantIdsAsync(CancellationToken cancellationToken) =>
        await controlPlane.Tenants
            .Where(tenant => tenant.Status != TenantStatus.Purging && tenant.Status != TenantStatus.Purged)
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken);

    void IFeatureFlagRepository.Add(FeatureFlag flag) => controlPlane.FeatureFlags.Add(flag);

    void IFeatureFlagRepository.List(FeatureFlagTenant listed) => controlPlane.FeatureFlagTenants.Add(listed);

    async Task IFeatureFlagRepository.UnlistAsync(Guid flagId, Guid tenantId, CancellationToken cancellationToken)
    {
        var listed = await controlPlane.FeatureFlagTenants.SingleAsync(row => row.FlagId == flagId && row.TenantId == tenantId, cancellationToken);
        controlPlane.FeatureFlagTenants.Remove(listed);
    }

    void IFeatureFlagRepository.Record(FeatureFlagChange change) => controlPlane.FeatureFlagChanges.Add(change);

    Task IFeatureFlagRepository.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
