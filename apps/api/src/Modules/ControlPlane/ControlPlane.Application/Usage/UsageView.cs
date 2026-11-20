namespace ControlPlane.Application.Usage;

/// <summary>
/// A tenant's use of the meters of its plan (11.01, 4.37): this calendar month of UTC, from <see cref="PeriodStart"/> to
/// <see cref="PeriodEnd"/>, and the months before it, the latest first. <see cref="SpendingCap"/> is the tenant's cap, offered only while
/// the tenant pays by card (<see cref="SpendingCapOffered"/>, E9).
/// </summary>
public sealed record UsageView(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string PlanName,
    IReadOnlyList<MeterUsageView> Meters,
    IReadOnlyList<PastUsageView> PastPeriods,
    SpendingCapView? SpendingCap,
    bool SpendingCapOffered);
