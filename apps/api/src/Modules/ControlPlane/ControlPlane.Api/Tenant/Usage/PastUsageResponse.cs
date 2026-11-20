namespace ControlPlane.Api.Tenant.Usage;

/// <summary>A month before this one, named by its first day: each meter of the plan, 0 for one without a use, and each meter it has a use of that the plan no longer sets.</summary>
public sealed record PastUsageResponse(DateOnly PeriodStart, IReadOnlyList<PastMeterUsageResponse> Meters);
