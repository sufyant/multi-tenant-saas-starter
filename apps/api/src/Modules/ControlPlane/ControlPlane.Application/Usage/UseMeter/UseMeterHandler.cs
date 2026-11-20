using ControlPlane.Contracts.Usage;
using ControlPlane.Domain.MeterUsages;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.SpendingCaps;

namespace ControlPlane.Application.Usage.UseMeter;

// The meter's setting comes from the tenant's plan, read under the tenant row's share lock in this transaction, never from the cache, as
// a limit is (E6, E8). A charge meter of a tenant that pays by card then locks the tenant's spending cap, and every use locks its month's
// row, so of two uses at once the second sees the first's count and extra, and a stop meter and the cap stop exactly (K6q). The month's
// extra is every other meter's at the price of its last use, and this meter's at the plan's price now, before rounding. A stopped use
// changes no count (W7); a use stopped by the cap marks the cap reached for the month (E9).
public static class UseMeterHandler
{
    public static async Task<MeterUse> HandleAsync(
        UseMeterCommand command,
        IMeterUsageRepository usages,
        ISpendingCapRepository caps,
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
        var period = MeterUsage.PeriodOf(now);
        var cap = setting.Overage == MeterOverage.Charge && command.PaysByCard ? await caps.FindForUpdateAsync(cancellationToken) : null;
        var usage = await usages.FindForUpdateAsync(command.MeterKey, period, cancellationToken);
        var extra = usage.ExtraAfter(command.Quantity, setting);
        if (cap is not null && extra > usage.Extra
            && !cap.Allows(await usages.SumExtraAmountAsync(period, usage.MeterKey, cancellationToken) + MeterUsage.AmountOf(extra, setting), period, now))
        {
            await caps.SaveChangesAsync(cancellationToken);
            return MeterUse.Stopped;
        }

        if (!usage.Use(command.Quantity, setting, now))
        {
            return MeterUse.Stopped;
        }

        await usages.SaveChangesAsync(cancellationToken);
        return MeterUse.Used;
    }
}
