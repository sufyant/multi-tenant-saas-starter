using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>
/// The details of <c>tenant.plan_cancelled</c>, in both logs: the plan the tenant pays for, the default plan it moves to and the moment it
/// moves, the end of the paid period (B9).
/// </summary>
public sealed record TenantPlanCancelledDetails(AuditPlan Plan, AuditPlan NewPlan, DateTimeOffset MovesAt) : AuditDetails;
