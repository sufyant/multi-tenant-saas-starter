using ControlPlane.Api.Platform.Plans;

namespace ControlPlane.Api.Tenant.Usage;

/// <summary>
/// One meter of the plan this month: the plan's quota and what happens at it, the price of the extra (<c>unitPrice</c> minor units for each
/// <c>unitSize</c> units) at the month's last use, the plan's before any, the units used, the extra over the quota of a charge meter and its
/// price in minor units at that price, rounded half away from zero, and the first moment the use reached the quota, null before.
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
