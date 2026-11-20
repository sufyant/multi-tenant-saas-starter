using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.Domain.MeterUsages;

/// <summary>
/// A tenant's use of one meter in one period, a calendar month of UTC that <see cref="Period"/> names by its first day (step 40c). It
/// counts every unit used, and the extra: the units used over the plan's quota of a charge meter. It keeps the price of the extra at its
/// last use, so a past month says what its extra cost after the plan's price changes, and the moment the count reached the quota.
/// </summary>
internal sealed class MeterUsage : ITenantEntity
{
    public MeterUsage(Guid tenantId, string meterKey, DateOnly period)
    {
        TenantId = tenantId;
        MeterKey = meterKey;
        Period = period;
    }

    public Guid TenantId { get; private init; }

    public string MeterKey { get; private init; }

    public DateOnly Period { get; private init; }

    public long Used { get; private set; }

    public long Extra { get; private set; }

    /// <summary>The price of the extra at the last use: <see cref="UnitPrice"/> minor units for each <see cref="UnitSize"/> units.</summary>
    public long UnitPrice { get; private set; }

    public long UnitSize { get; private set; } = 1;

    /// <summary>
    /// The first moment the count reached the quota in the period; none before. It stays when a plan change raises the quota later, as the
    /// notice at 100% goes once in each period.
    /// </summary>
    public DateTimeOffset? QuotaReachedAt { get; private set; }

    /// <summary>The first moment the count reached 80% of the quota in the period; none before. The notice at 80% goes once (E10).</summary>
    public DateTimeOffset? NearlyUsedAt { get; private set; }

    /// <summary>The period of a moment: the first day of its month in UTC.</summary>
    public static DateOnly PeriodOf(DateTimeOffset moment)
    {
        var utc = moment.UtcDateTime;
        return new DateOnly(utc.Year, utc.Month, 1);
    }

    /// <summary>What an extra costs at the setting's price, in minor units before rounding.</summary>
    public static decimal AmountOf(long extra, MeterSetting setting) => (decimal)extra * setting.UnitPrice / setting.UnitSize;

    /// <summary>
    /// Counts the use, unless the meter stops at the quota and the use would take the count over it: then it counts nothing and answers
    /// <see cref="MeterCount.Stopped"/>. The part of a charge meter's use over the quota is extra. A use that first takes the count to the
    /// quota answers <see cref="MeterCount.UsedUp"/>, and one that first takes it to 80% of the quota <see cref="MeterCount.NearlyUsed"/>;
    /// a quota of 0 tells of neither (E10).
    /// </summary>
    public MeterCount Use(long quantity, MeterSetting setting, DateTimeOffset now)
    {
        var used = Used + quantity;
        if (setting.Stops(used))
        {
            return MeterCount.Stopped;
        }

        Extra = ExtraAfter(quantity, setting);
        Used = used;
        (UnitPrice, UnitSize) = (setting.UnitPrice, setting.UnitSize);
        var (reached, nearly) = (QuotaReachedAt is null && used >= setting.Quota, NearlyUsedAt is null && used * 5 >= setting.Quota * 4);
        QuotaReachedAt ??= reached ? now : null;
        NearlyUsedAt ??= nearly ? now : null;
        return (setting.Quota, reached, nearly) switch
        {
            (0, _, _) => MeterCount.Counted,
            (_, true, _) => MeterCount.UsedUp,
            (_, _, true) => MeterCount.NearlyUsed,
            _ => MeterCount.Counted,
        };
    }

    /// <summary>The extra after a use of this quantity: the part of the count over the quota that this use adds, and what it had.</summary>
    public long ExtraAfter(long quantity, MeterSetting setting) => Extra + Math.Max(0, Used + quantity - Math.Max(Used, setting.Quota));
}
