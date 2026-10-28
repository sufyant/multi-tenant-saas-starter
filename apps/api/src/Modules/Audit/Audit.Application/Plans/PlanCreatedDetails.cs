using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>The details of <c>plan.created</c>: whether the plan took the default mark, and its value of each key it has one for.</summary>
public sealed record PlanCreatedDetails(bool IsDefault, IReadOnlyDictionary<string, AuditEntitlementValue> Entitlements) : AuditDetails;
