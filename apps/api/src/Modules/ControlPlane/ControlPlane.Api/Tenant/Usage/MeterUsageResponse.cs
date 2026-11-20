using ControlPlane.Api.Platform.Plans;

namespace ControlPlane.Api.Tenant.Usage;

/// <summary>
/// One meter of the plan this month: the plan's setting of it (the quota, what happens at the quota, and <c>unitPrice</c> minor units for
/// each <c>unitSize</c> units of extra), the units used, the extra over the quota of a charge meter and its price in minor units, rounded
/// half away from zero, and when the use reached the quota, null before.
/// </summary>
public sealed record MeterUsageResponse(
    string Key,
    long Quota,
    MeterSettingResponseOverage Overage,
    long UnitPrice,
    long UnitSize,
    long Used,
    long Extra,
    long ExtraAmount,
    DateTimeOffset? QuotaReachedAt);
