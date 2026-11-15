using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>
/// The details of <c>plan.created</c>: whether the plan took the default mark, its value of each key it has one for, its prices and its
/// meters. An entry from before step 40 has no prices and no meters.
/// </summary>
public sealed record PlanCreatedDetails(
    bool IsDefault,
    IReadOnlyDictionary<string, AuditEntitlementValue> Entitlements,
    AuditPlanPrices? Prices = null,
    IReadOnlyList<AuditMeterSetting>? Meters = null) : AuditDetails;
