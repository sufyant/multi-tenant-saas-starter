using ControlPlane.Application.Usage;
using ControlPlane.Domain.MeterUsages;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Usage;

/// <summary>
/// The reads of a tenant's use. The tenant is the transaction's (W2): the tenant, its plan and the plan's meters belong to no tenant (R6), and
/// the uses are the tenant's rows.
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class UsageQueries(ControlPlaneDbContext controlPlane) : IUsageQueries
{
    async Task<UsageView?> IUsageQueries.FindUsageAsync(DateOnly period, int pastMonths, CancellationToken cancellationToken)
    {
        if (controlPlane.MessageTenantId is not { } tenantId)
        {
            return null;
        }

        var tenant = await controlPlane.Tenants
            .Where(found => found.Id == tenantId)
            .Join(controlPlane.Plans, found => found.PlanId, plan => plan.Id, (found, plan) => new { found.CreatedAt, PlanName = plan.Name, PlanId = plan.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var meters = await controlPlane.PlanMeters.Where(meter => meter.PlanId == tenant.PlanId).OrderBy(meter => meter.Key).ToListAsync(cancellationToken);
        var first = new[] { period.AddMonths(-pastMonths), MeterUsage.PeriodOf(tenant.CreatedAt) }.Max();
        var uses = await controlPlane.MeterUsages.AsNoTracking().Where(use => use.Period >= first).ToListAsync(cancellationToken);
        var useOf = uses.ToDictionary(use => (use.MeterKey, use.Period));

        // A line with a use answers the price of the last use, which its amount was counted at; the next use takes the plan's price.
        var current = meters.Select(meter => useOf.GetValueOrDefault((meter.Key, period)) is { } use
            ? new MeterUsageView(meter.Key, meter.Quota, meter.Overage.ToString(), use.UnitPrice, use.UnitSize, use.Used, use.Extra, AmountOf(use), use.QuotaReachedAt)
            : new MeterUsageView(meter.Key, meter.Quota, meter.Overage.ToString(), meter.UnitPrice, meter.UnitSize, 0, 0, 0, null));

        // A past month lists the plan's meters and every meter it has a use of, so a meter that a plan change took away keeps its months.
        var past = Enumerable.Range(1, pastMonths)
            .Select(months => period.AddMonths(-months))
            .Where(month => month >= first)
            .Select(month => new PastUsageView(month, [.. meters.Select(meter => meter.Key)
                .Union(uses.Where(use => use.Period == month).Select(use => use.MeterKey))
                .Order(StringComparer.Ordinal)
                .Select(key => useOf.GetValueOrDefault((key, month)) is { } use
                    ? new PastMeterUsageView(key, use.Used, use.Extra, AmountOf(use))
                    : new PastMeterUsageView(key, 0, 0, 0))]));

        var cap = await controlPlane.SpendingCaps.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var spendingCap = cap is null
            ? null
            : new SpendingCapView(cap.Amount, uses.Where(use => use.Period == period).Sum(AmountOf), cap.ReachedIn(period));

        return new UsageView(period, period.AddMonths(1).AddDays(-1), tenant.PlanName, [.. current], [.. past], spendingCap, SpendingCapOffered: false);
    }

    // The extra's price in minor units, rounded half away from zero: 600 requests at 100 for each 1000 cost 60.
    private static long AmountOf(MeterUsage use) =>
        (long)Math.Round((decimal)use.Extra * use.UnitPrice / use.UnitSize, MidpointRounding.AwayFromZero);
}
