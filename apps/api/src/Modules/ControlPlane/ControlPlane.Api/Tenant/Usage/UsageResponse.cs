namespace ControlPlane.Api.Tenant.Usage;

/// <summary>
/// The tenant's use of the meters of its plan (step 40c; 11.01, 4.37): this calendar month of UTC, from <c>periodStart</c> to
/// <c>periodEnd</c>, its plan's name, the deployment's currency, an ISO 4217 code, each meter of the plan, and up to three months before
/// this one, from the month the tenant was created, the latest first. A plan with no meter answers none.
/// </summary>
public sealed record UsageResponse(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string PlanName,
    string Currency,
    IReadOnlyList<MeterUsageResponse> Meters,
    IReadOnlyList<PastUsageResponse> PastPeriods);
