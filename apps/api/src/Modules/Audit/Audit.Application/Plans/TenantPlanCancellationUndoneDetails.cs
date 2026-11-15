using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>The details of <c>tenant.plan_cancellation_undone</c>, in both logs: the plan the tenant stays on.</summary>
public sealed record TenantPlanCancellationUndoneDetails(AuditPlan Plan) : AuditDetails;
