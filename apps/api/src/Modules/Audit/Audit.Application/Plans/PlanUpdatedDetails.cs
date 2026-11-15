using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>
/// The details of <c>plan.updated</c>: the plan's old and new name, default mark, values, prices and meters, and, when the default mark
/// moved to this plan, the plan that had it. An entry from before step 40 has no prices and no meters. The target keeps the new name.
/// </summary>
public sealed record PlanUpdatedDetails(
    string OldName,
    string NewName,
    bool WasDefault,
    bool IsDefault,
    IReadOnlyDictionary<string, AuditEntitlementValue> OldEntitlements,
    IReadOnlyDictionary<string, AuditEntitlementValue> NewEntitlements,
    AuditPlan? PreviousDefaultPlan,
    AuditPlanPrices? OldPrices = null,
    AuditPlanPrices? NewPrices = null,
    IReadOnlyList<AuditMeterSetting>? OldMeters = null,
    IReadOnlyList<AuditMeterSetting>? NewMeters = null) : AuditDetails;
