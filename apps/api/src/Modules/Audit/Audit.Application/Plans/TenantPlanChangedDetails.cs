using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>The details of <c>tenant.plan_changed</c>: the old and the new plan, as they were at that moment, in both logs.</summary>
public sealed record TenantPlanChangedDetails(AuditPlan OldPlan, AuditPlan NewPlan) : AuditDetails;
