namespace ControlPlane.Application.Usage;

/// <summary>
/// A tenant's use of the meters of its plan (11.01, 4.37): this calendar month of UTC, from <see cref="PeriodStart"/> to
/// <see cref="PeriodEnd"/>, and the months before it, the latest first.
/// </summary>
public sealed record UsageView(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string PlanName,
    IReadOnlyList<MeterUsageView> Meters,
    IReadOnlyList<PastUsageView> PastPeriods);
