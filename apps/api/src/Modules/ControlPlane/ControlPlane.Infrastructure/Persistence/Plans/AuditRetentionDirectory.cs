using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// K14: each tenant's Activity log period, from its plan's value and its override of the key, in one query across the tenants: the tenants,
// the plans' values and the overrides belong to no tenant (R6), so no transaction declares one. The effective value is computed with the
// moment of the call (E3), so an override stops at its expiry and a missing value keeps every entry. A purging or a purged tenant is left
// out: its log goes with the purge (R14). It reads past the cache, as a limit is checked (E6). Public because Wolverine builds it in
// Audit's handlers of the retention, on this module's DbContext (W1, W9).
public sealed class AuditRetentionDirectory(ControlPlaneDbContext controlPlane, TimeProvider time) : IAuditRetentionDirectory
{
    private const string Key = LimitKeys.AuditRetentionDays;

    public async Task<IReadOnlyList<TenantAuditRetention>> ListAsync(CancellationToken cancellationToken) =>
        await PeriodsAsync(controlPlane.Tenants, cancellationToken);

    public async Task<TenantAuditRetention?> FindAsync(Guid tenantId, CancellationToken cancellationToken) =>
        (await PeriodsAsync(controlPlane.Tenants.Where(tenant => tenant.Id == tenantId), cancellationToken)).SingleOrDefault();

    private async Task<List<TenantAuditRetention>> PeriodsAsync(IQueryable<Tenant> tenants, CancellationToken cancellationToken)
    {
        var rows = await (
                from tenant in tenants
                where tenant.Status != TenantStatus.Purging && tenant.Status != TenantStatus.Purged
                join planValue in controlPlane.PlanEntitlements.Where(value => value.Key == Key) on tenant.PlanId equals planValue.PlanId into planValues
                from planValue in planValues.DefaultIfEmpty()
                join entry in controlPlane.EntitlementOverrides.Where(entry => entry.Key == Key) on tenant.Id equals entry.TenantId into entries
                from entry in entries.DefaultIfEmpty()
                select new
                {
                    tenant.Id,
                    PlanMaximum = planValue.Maximum,
                    PlanUnlimited = (bool?)planValue.Unlimited,
                    OverrideMaximum = entry.Maximum,
                    OverrideUnlimited = (bool?)entry.Unlimited,
                    entry.ExpiresAt,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow();
        return
        [
            .. rows.Select(row =>
            {
                Dictionary<string, EntitlementValue> plan = row.PlanUnlimited is { } planUnlimited
                    ? new(StringComparer.Ordinal) { [Key] = EntitlementValue.Read(null, row.PlanMaximum, planUnlimited) }
                    : new(StringComparer.Ordinal);
                OverrideValue[] overrides = row.OverrideUnlimited is { } overrideUnlimited
                    ? [new OverrideValue(Key, EntitlementValue.Read(null, row.OverrideMaximum, overrideUnlimited), row.ExpiresAt)]
                    : [];
                return new TenantAuditRetention(row.Id, EffectiveEntitlements.Of(plan, overrides, now).ValueOf(Key).Maximum);
            }),
        ];
    }
}
