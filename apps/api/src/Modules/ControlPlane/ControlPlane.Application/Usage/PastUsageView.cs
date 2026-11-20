namespace ControlPlane.Application.Usage;

/// <summary>A month before this one: the use of each meter of the plan, 0 for a meter without a use.</summary>
public sealed record PastUsageView(DateOnly PeriodStart, IReadOnlyList<PastMeterUsageView> Meters);
