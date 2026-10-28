using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Tenancy.Caching;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// A tenant's effective entitlements from two cached reads: the tenant's plan and overrides, and the plan's values, each with its own tag,
// so a plan edit removes only the plan's entry and every tenant on it sees the edit at once, while a tenant on another plan keeps its
// entries (C5). The effective value is computed here with the moment of the call, so an override stops at its expiry without a removal.
// Plans, their values, overrides and tenants belong to no tenant (R6), so no transaction declares one.
internal sealed class EntitlementDirectory(ControlPlaneDbContext controlPlane, ReadCache cache, TimeProvider time) : IEntitlementDirectory
{
    public async Task<TenantEntitlements> FindAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var effective = await EffectiveAsync(tenantId, cancellationToken);
        return new TenantEntitlements(
            EntitlementKeys.Features.ToDictionary(key => key, key => effective.ValueOf(key).IsOn, StringComparer.Ordinal),
            EntitlementKeys.Limits.ToDictionary(key => key, key => effective.ValueOf(key).Maximum, StringComparer.Ordinal));
    }

    public async Task<Result> CheckFeatureAsync(Guid tenantId, string feature, CancellationToken cancellationToken) =>
        (await EffectiveAsync(tenantId, cancellationToken)).CheckFeature(feature);

    private static EntitlementValue ValueOf(EntitlementRow row) => EntitlementValue.Read(row.Enabled, row.Maximum, row.Unlimited);

    private async Task<EffectiveEntitlements> EffectiveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await cache.ReadAsync(CachedReads.TenantEntitlements, tenantId, token => ReadTenantAsync(tenantId, token), cancellationToken);
        var plan = await cache.ReadAsync(CachedReads.PlanEntitlements, tenant.PlanId, token => ReadPlanAsync(tenant.PlanId, token), cancellationToken);
        return EffectiveEntitlements.Of(
            plan.Values.ToDictionary(row => row.Key, ValueOf, StringComparer.Ordinal),
            tenant.Overrides.Select(row => new OverrideValue(row.Key, ValueOf(row), row.ExpiresAt)),
            time.GetUtcNow());
    }

    private async Task<TenantEntitlementRows> ReadTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var planId = await controlPlane.Tenants.Where(tenant => tenant.Id == tenantId).Select(tenant => tenant.PlanId).SingleAsync(cancellationToken);
        var overrides = await controlPlane.EntitlementOverrides
            .Where(entry => entry.TenantId == tenantId)
            .OrderBy(entry => entry.Key)
            .Select(entry => new EntitlementRow(entry.Key, entry.Enabled, entry.Maximum, entry.Unlimited, entry.ExpiresAt))
            .ToArrayAsync(cancellationToken);
        return new TenantEntitlementRows(planId, overrides);
    }

    private async Task<PlanEntitlementRows> ReadPlanAsync(Guid planId, CancellationToken cancellationToken) =>
        new(await controlPlane.PlanEntitlements
            .Where(value => value.PlanId == planId)
            .OrderBy(value => value.Key)
            .Select(value => new EntitlementRow(value.Key, value.Enabled, value.Maximum, value.Unlimited, null))
            .ToArrayAsync(cancellationToken));
}
