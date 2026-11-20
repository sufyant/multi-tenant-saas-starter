namespace ControlPlane.Application.Usage;

/// <summary>
/// The use of one meter of the plan this month, with the plan's setting of it (<see cref="Overage"/> <c>Stop</c> or <c>Charge</c>). The
/// extra costs <see cref="ExtraAmount"/> minor units, rounded half away from zero; <see cref="QuotaReachedAt"/> is when the use reached the
/// quota.
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
