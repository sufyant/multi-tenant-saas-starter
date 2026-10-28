using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>The details of <c>plan.updated</c>: the plan's old and new name, default mark and values. The target keeps the new name.</summary>
public sealed record PlanUpdatedDetails(
    string OldName,
    string NewName,
    bool WasDefault,
    bool IsDefault,
    IReadOnlyDictionary<string, AuditEntitlementValue> OldEntitlements,
    IReadOnlyDictionary<string, AuditEntitlementValue> NewEntitlements) : AuditDetails;
