using ControlPlane.Contracts.Usage;
using ControlPlane.Domain.MeterUsages;

namespace ControlPlane.Application.Usage.UseMeter;

// The meter's setting comes from the tenant's plan, read in this transaction, never from the cache (E-rules: a limit is checked from the
// database). The use's row is locked, so of two uses at once the second sees the first's count, and a stop meter stops exactly at its
// quota. A stopped use changes nothing (W7).
public static class UseMeterHandler
{
    public static async Task<MeterUse> HandleAsync(
        UseMeterCommand command,
        IMeterUsageRepository usages,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(command.Quantity, 1);

        var setting = await usages.FindSettingAsync(command.MeterKey, cancellationToken);
        if (setting is null)
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
