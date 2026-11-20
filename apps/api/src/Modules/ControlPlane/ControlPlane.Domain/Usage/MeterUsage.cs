using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.Domain.Usage;

/// <summary>
/// A tenant's use of one meter in one period, a calendar month of UTC that <see cref="Period"/> names by its first day (step 40c). It
/// counts every unit used, and the extra: the units used over the plan's quota of a charge meter.
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

    /// <summary>The period of a moment: the first day of its month in UTC.</summary>
    public static DateOnly PeriodOf(DateTimeOffset moment)
    {
        var utc = moment.UtcDateTime;
        return new DateOnly(utc.Year, utc.Month, 1);
    }

    /// <summary>
    /// Counts the use, unless the meter stops at the quota and the use would take the count over it: then it counts nothing and answers
    /// false. The part of a charge meter's use over the quota is extra.
    /// </summary>
    public bool Use(long quantity, MeterSetting setting)
    {
        var used = Used + quantity;
        if (setting.Overage == MeterOverage.Stop && used > setting.Quota)
        {
            return false;
        }

        Extra += Math.Max(0, used - Math.Max(Used, setting.Quota));
        Used = used;
        return true;
    }
}
