using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Usage;
using ControlPlane.Domain.MeterUsages;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.SpendingCaps;
using Wolverine;

namespace ControlPlane.Application.Usage.UseMeter;

// The meter's setting comes from the tenant's plan, read under the tenant row's share lock in this transaction, never from the cache, as
// a limit is (E6, E8). A charge meter of a tenant that pays by card then locks the tenant's spending cap, and every use locks its month's
// row, so of two uses at once the second sees the first's count and extra, and a stop meter and the cap stop exactly (K6q). The month's
// extra is every other meter's at the price of its last use, and this meter's at the plan's price now, before rounding. A stopped use
// changes no count (W7); a use stopped by the cap marks the cap reached for the month (E9). The first use of the month that takes the count
// to 80% or to all of the quota, or the extra to 80% or to all of the cap, publishes its notice's event once (E10).
public static class UseMeterHandler
{
    public static async Task<(MeterUse, OutgoingMessages)> HandleAsync(
        UseMeterCommand command,
        IMeterUsageRepository usages,
        ISpendingCapRepository caps,
        BillingOptions billing,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(command.Quantity, 1);

        // A first use of the period counts from nothing, so a use that alone goes over a stop meter's quota is stopped before its row is
        // written (W7).
        var setting = await usages.FindSettingForShareAsync(command.MeterKey, cancellationToken);
        if (setting is null || setting.Stops(command.Quantity))
        {
            return (MeterUse.Stopped, []);
        }

        var now = time.GetUtcNow();
        var period = MeterUsage.PeriodOf(now);
        var cap = setting.Overage == MeterOverage.Charge && command.PaysByCard ? await caps.FindForUpdateAsync(cancellationToken) : null;
        var usage = await usages.FindForUpdateAsync(command.MeterKey, period, cancellationToken);
        var notices = new Notices(usage, setting, billing.Currency, now);
        var extra = usage.ExtraAfter(command.Quantity, setting);
        OutgoingMessages messages = [];
        if (cap is not null && extra > usage.Extra)
        {
            var amount = await usages.SumExtraAmountAsync(period, usage.MeterKey, cancellationToken) + MeterUsage.AmountOf(extra, setting);
            var (allowed, capNotice) = cap.Use(amount, period, now);
            messages.AddRange(notices.Of(capNotice, cap.Amount, amount));
            if (!allowed)
            {
                await caps.SaveChangesAsync(cancellationToken);
                return (MeterUse.Stopped, messages);
            }
        }

        var count = usage.Use(command.Quantity, setting, now);
        if (count == MeterCount.Stopped)
        {
            return (MeterUse.Stopped, []);
        }

        messages.AddRange(notices.Of(count));
        await usages.SaveChangesAsync(cancellationToken);
        return (MeterUse.Used, messages);
    }

    // The events of the notices of one use, each with the meter's price for a charge meter and the day the month ends.
    private sealed class Notices(MeterUsage usage, MeterSetting setting, string currency, DateTimeOffset now)
    {
        private readonly (long? Price, long? Size) _price = setting.Overage == MeterOverage.Charge ? (setting.UnitPrice, setting.UnitSize) : (null, null);

        private DateOnly ResetsOn => usage.Period.AddMonths(1);

        public IEnumerable<object> Of(MeterCount count) => count switch
        {
            MeterCount.NearlyUsed =>
                [new MeterNearlyUsedEvent(Guid.CreateVersion7(now), now, usage.TenantId, usage.MeterKey, usage.Used, setting.Quota, _price.Price, _price.Size, currency, ResetsOn)],
            MeterCount.UsedUp =>
                [new MeterUsedUpEvent(Guid.CreateVersion7(now), now, usage.TenantId, usage.MeterKey, setting.Quota, _price.Price, _price.Size, currency, ResetsOn)],
            _ => [],
        };

        public IEnumerable<object> Of(CapNotice notice, long cap, decimal amount) => notice switch
        {
            CapNotice.NearlyReached =>
                [new SpendingCapNearlyReachedEvent(Guid.CreateVersion7(now), now, usage.TenantId, cap, (long)Math.Round(amount, MidpointRounding.AwayFromZero), currency, ResetsOn)],
            CapNotice.Reached => [new SpendingCapReachedEvent(Guid.CreateVersion7(now), now, usage.TenantId, usage.MeterKey, cap, currency, ResetsOn)],
            _ => [],
        };
    }
}
