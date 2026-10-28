using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>The details of <c>tenant.plan_changed</c>: the old and the new plan, each by id and by its name of that moment, in both logs.</summary>
public sealed record TenantPlanChangedDetails(Guid OldPlanId, string OldPlanName, Guid NewPlanId, string NewPlanName) : AuditDetails;
