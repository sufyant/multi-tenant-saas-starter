namespace ControlPlane.Application.Usage;

/// <summary>
/// The use of one meter of the plan this month, with the plan's quota and overage (<see cref="Overage"/> <c>Stop</c> or <c>Charge</c>)
/// and the price of the extra at the last use, the plan's before any. The extra costs <see cref="ExtraAmount"/> minor units at that price,
/// rounded half away from zero; <see cref="QuotaReachedAt"/> is the first moment the use reached the quota.
/// </summary>
public sealed record MeterUsageView(
    string Key,
    long Quota,
    string Overage,
    long UnitPrice,
    long UnitSize,
    long Used,
    long Extra,
    long ExtraAmount,
    DateTimeOffset? QuotaReachedAt);
