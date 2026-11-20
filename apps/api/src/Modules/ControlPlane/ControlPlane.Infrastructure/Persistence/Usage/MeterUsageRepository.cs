using ControlPlane.Application.Usage;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Usage;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Usage;

/// <summary>
/// A tenant's use of its meters. The tenant is the transaction's (W2): the tenant row and the plan's meters belong to no tenant (R6), and the
/// use's row is the tenant's.
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class MeterUsageRepository(ControlPlaneDbContext controlPlane) : IMeterUsageRepository
{
    async Task<MeterSetting?> IMeterUsageRepository.FindSettingAsync(string meterKey, CancellationToken cancellationToken)
    {
        var tenantId = controlPlane.MessageTenantId ?? throw new InvalidOperationException("A meter is used in a tenant's transaction (R4).");
        var meter = await controlPlane.Tenants
            .Where(tenant => tenant.Id == tenantId)
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
            $"INSERT INTO control_plane.meter_usages (meter_key, period, used, extra) VALUES ({meterKey}, {period}, 0, 0) ON CONFLICT DO NOTHING",
            cancellationToken);
        return await controlPlane.MeterUsages
            .FromSql($"SELECT * FROM control_plane.meter_usages WHERE meter_key = {meterKey} AND period = {period} FOR UPDATE")
            .SingleAsync(cancellationToken);
    }

    Task IMeterUsageRepository.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
