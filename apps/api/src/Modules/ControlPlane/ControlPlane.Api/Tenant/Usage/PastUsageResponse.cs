namespace ControlPlane.Api.Tenant.Usage;

/// <summary>A month before this one, named by its first day: each meter of the plan, 0 for one without a use.</summary>
public sealed record PastUsageResponse(DateOnly PeriodStart, IReadOnlyList<PastMeterUsageResponse> Meters);
