using ControlPlane.Application.FeatureFlags;
using ControlPlane.Application.Users;
using ControlPlane.Domain.FeatureFlags;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.FeatureFlags;

/// <summary>The platform's reads of the feature flags (Q2). Flags, tenants and users belong to no tenant (R6), so each read sees every row.</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class FeatureFlagQueries(ControlPlaneDbContext controlPlane) : IFeatureFlagQueries
{
    async Task<IReadOnlyList<FeatureFlagListItemView>> IFeatureFlagQueries.ListFlagsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var flags = await controlPlane.FeatureFlags.AsNoTracking().OrderBy(flag => flag.Key).ToListAsync(cancellationToken);
        var names = await NamesOfAsync([.. flags.Select(flag => flag.LastChangedBy)], cancellationToken);
        return [.. flags.Select(flag => ItemOf(flag, now, names))];
    }

    async Task<FeatureFlagDetailView?> IFeatureFlagQueries.FindFlagAsync(string key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await controlPlane.FeatureFlags.AsNoTracking().SingleOrDefaultAsync(flag => flag.Key == key, cancellationToken) is not { } flag)
        {
            return null;
        }

        var rows = await controlPlane.FeatureFlagTenants.AsNoTracking().Where(row => row.FlagId == flag.Id).ToListAsync(cancellationToken);
        var changes = await controlPlane.FeatureFlagChanges.AsNoTracking()
            .Where(change => change.FlagId == flag.Id)
            .OrderByDescending(change => change.At)
            .ThenByDescending(change => change.Id)
            .ToListAsync(cancellationToken);
        var tenantIds = rows.Select(row => row.TenantId).Concat(changes.Where(change => change.TenantId is not null).Select(change => change.TenantId!.Value)).Distinct().ToList();
        var tenantNames = await controlPlane.Tenants.AsNoTracking()
            .Where(tenant => tenantIds.Contains(tenant.Id))
            .ToDictionaryAsync(tenant => tenant.Id, tenant => tenant.Name, cancellationToken);
        var listed = rows.Select(row => new FeatureFlagTenantView(row.TenantId, tenantNames[row.TenantId], row.ListedAt)).OrderBy(row => row.Name, StringComparer.CurrentCulture).ToList();
        var names = await NamesOfAsync([flag.LastChangedBy, .. changes.Select(change => change.ActorId)], cancellationToken);
        return new FeatureFlagDetailView(
            ItemOf(flag, now, names),
            flag.Description,
            flag.DefaultValue,
            flag.AddedAt,
            flag.AddedRevision,
            flag.RemovedRevision,
            listed,
            [.. changes.Select(change => new FeatureFlagChangeView(
                change.Kind.ToString(),
                change.From,
                change.To,
                change.TenantId is { } tenantId ? new FeatureFlagTenantNameView(tenantId, tenantNames[tenantId]) : null,
                change.Revision,
                change.At,
                change.ActorId is { } actor ? names[actor] : null))]);
    }

    async Task<TenantFeatureFlagView[]?> IFeatureFlagQueries.ListOnForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!await controlPlane.Tenants.AnyAsync(tenant => tenant.Id == tenantId, cancellationToken))
        {
            return null;
        }

        var flags = await controlPlane.FeatureFlags.AsNoTracking().Where(flag => flag.RemovedAt == null).OrderBy(flag => flag.Key).ToListAsync(cancellationToken);
        var listed = (await controlPlane.FeatureFlagTenants.Where(row => row.TenantId == tenantId).Select(row => row.FlagId).ToListAsync(cancellationToken)).ToHashSet();
        return
        [
            .. flags
                .Select(flag => (Flag: flag, Reason: flag.RulesFor(listed.Contains(flag.Id)).ReasonFor(tenantId)))
                .Where(on => on.Reason is not null)
                .Select(on => new TenantFeatureFlagView(on.Flag.Key, on.Flag.Type.ToString(), on.Reason!.Value.ToString(), on.Flag.Percentage)),
        ];
    }

    private static FeatureFlagListItemView ItemOf(FeatureFlag flag, DateTimeOffset now, IReadOnlyDictionary<Guid, UserNameView> names)
    {
        var cleanup = flag.CleanupAt(now);
        return new FeatureFlagListItemView(
            flag.Key,
            flag.Type.ToString(),
            flag.ListedCount,
            flag.Percentage,
            flag.EveryoneElse,
            cleanup?.ToString(),
            cleanup is null ? null : flag.StateSince,
            flag.RemovedAt,
            flag.LastChangedAt,
            flag.LastChangedBy is { } actor ? names[actor] : null);
    }

    private async Task<IReadOnlyDictionary<Guid, UserNameView>> NamesOfAsync(IReadOnlyCollection<Guid?> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.OfType<Guid>().Distinct().ToList();
        return await controlPlane.Users.AsNoTracking().Where(user => ids.Contains(user.Id)).ToDictionaryAsync(user => user.Id, UserNameView.Of, cancellationToken);
    }
}
