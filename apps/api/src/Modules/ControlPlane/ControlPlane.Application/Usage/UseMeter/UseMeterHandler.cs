using ControlPlane.Contracts.Usage;
using ControlPlane.Domain.MeterUsages;

namespace ControlPlane.Application.Usage.UseMeter;

// The meter's setting comes from the tenant's plan, read under the tenant row's share lock in this transaction, never from the cache, as
// a limit is (E6, E8). Then the use's row is locked, so of two uses at once the second sees the first's count, and a stop meter stops
// exactly at its quota (K6q). A stopped use changes nothing (W7).
public static class UseMeterHandler
{
    public static async Task<MeterUse> HandleAsync(
        UseMeterCommand command,
        IMeterUsageRepository usages,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(command.Quantity, 1);

        // A first use of the period counts from nothing, so a use that alone goes over a stop meter's quota is stopped before its row is
        // written (W7).
        var setting = await usages.FindSettingForShareAsync(command.MeterKey, cancellationToken);
        if (setting is null || setting.Stops(command.Quantity))
        {
            return MeterUse.Stopped;
        }

        var now = time.GetUtcNow();
        var usage = await usages.FindForUpdateAsync(command.MeterKey, MeterUsage.PeriodOf(now), cancellationToken);
        if (!usage.Use(command.Quantity, setting, now))
        {
            return MeterUse.Stopped;
        }

        await usages.SaveChangesAsync(cancellationToken);
        return MeterUse.Used;
    }
}
