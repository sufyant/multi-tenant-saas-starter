using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// The reads that the platform's views of plans and of a tenant's plan share. Plans, their values, overrides and the tenants' summaries
// belong to no tenant (R6), so each read sees every row it names, across tenants too.
internal static class EntitlementReads
{
    public static async Task<Dictionary<string, EntitlementValue>> PlanValuesAsync(this ControlPlaneDbContext controlPlane, Guid planId, CancellationToken cancellationToken) =>
        (await controlPlane.PlanEntitlements.Where(value => value.PlanId == planId).AsNoTracking().ToListAsync(cancellationToken))
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);

    public static async Task<ILookup<Guid, EntitlementOverride>> OverridesOfAsync(
        this ControlPlaneDbContext controlPlane,
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken) =>
        (await controlPlane.EntitlementOverrides.Where(entry => tenantIds.Contains(entry.TenantId)).AsNoTracking().ToListAsync(cancellationToken))
            .ToLookup(entry => entry.TenantId);

    // What the tenant has that the keys count, from its summary (R12), at this moment. A purged tenant has no summary, and counts nothing.
    public static EntitlementUsage UsageOf(TenantSummary? summary, DateTimeOffset now) =>
        summary is null ? new EntitlementUsage(0, 0, 0) : new EntitlementUsage(summary.CustomRoleCount, summary.MemberCount, summary.PendingInvitationsAt(now));
}
