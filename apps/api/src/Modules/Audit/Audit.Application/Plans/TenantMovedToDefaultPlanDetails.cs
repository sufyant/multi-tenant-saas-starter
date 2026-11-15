using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>
/// The details of <c>tenant.moved_to_default_plan</c>, in both logs: the plan the unpaid subscription paid for and the default plan the
/// tenant moved to (B11).
/// </summary>
public sealed record TenantMovedToDefaultPlanDetails(AuditPlan OldPlan, AuditPlan NewPlan) : AuditDetails;
