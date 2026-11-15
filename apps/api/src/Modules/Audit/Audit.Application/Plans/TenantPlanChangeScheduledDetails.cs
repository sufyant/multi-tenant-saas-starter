using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>
/// The details of <c>tenant.plan_change_scheduled</c>, in both logs: the plan the tenant pays for, the plan it moves to and the moment it
/// moves, the end of the paid period (B9). The same plan twice is a move from yearly to monthly.
/// </summary>
public sealed record TenantPlanChangeScheduledDetails(AuditPlan OldPlan, AuditPlan NewPlan, DateTimeOffset AppliesAt) : AuditDetails;
