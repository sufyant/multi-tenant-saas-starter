using ControlPlane.Application.Usage;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.MeterUsages;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Usage;

/// <summary>
/// A tenant's use of its meters. The tenant is the transaction's (W2): the tenant row and the plan's meters belong to no tenant (R6), and the
/// use's row is the tenant's.
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class MeterUsageRepository(ControlPlaneDbContext controlPlane) : IMeterUsageRepository
{
    // Only an active tenant uses its meters: a suspended one is locked out of the tenant door, and a closed one waits for its purge.
    async Task<MeterSetting?> IMeterUsageRepository.FindSettingForShareAsync(string meterKey, CancellationToken cancellationToken)
    {
        var tenantId = controlPlane.MessageTenantId ?? throw new InvalidOperationException("A meter is used in a tenant's transaction (R4).");
        var meter = await controlPlane.Tenants
            .FromSql($"SELECT * FROM control_plane.tenants WHERE id = {tenantId} AND status = 'Active' FOR SHARE")
            .Join(controlPlane.PlanMeters, tenant => tenant.PlanId, planMeter => planMeter.PlanId, (_, planMeter) => planMeter)
            .Where(planMeter => planMeter.Key == meterKey)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return meter?.Setting;
    }

    // The insert of a month's first use and the lock of its row: of two first uses at once, one inserts and the other waits on its lock.
    async Task<MeterUsage> IMeterUsageRepository.FindForUpdateAsync(string meterKey, DateOnly period, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync(
            $"INSERT INTO control_plane.meter_usages (meter_key, period, used, extra, unit_price, unit_size) VALUES ({meterKey}, {period}, 0, 0, 0, 1) ON CONFLICT DO NOTHING",
            cancellationToken);
        return await controlPlane.MeterUsages
            .FromSql($"SELECT * FROM control_plane.meter_usages WHERE meter_key = {meterKey} AND period = {period} FOR UPDATE")
            .SingleAsync(cancellationToken);
    }

    async Task<decimal> IMeterUsageRepository.SumExtraAmountAsync(DateOnly period, string exceptMeterKey, CancellationToken cancellationToken) =>
        await controlPlane.MeterUsages
            .Where(use => use.Period == period && use.MeterKey != exceptMeterKey)
            .SumAsync(use => (decimal)use.Extra * use.UnitPrice / use.UnitSize, cancellationToken);

    Task IMeterUsageRepository.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
