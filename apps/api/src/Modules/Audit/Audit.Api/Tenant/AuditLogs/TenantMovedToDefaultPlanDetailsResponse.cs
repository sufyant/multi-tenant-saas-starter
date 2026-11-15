namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantMovedToDefaultPlanDetailsResponse(AuditPlanResponse OldPlan, AuditPlanResponse NewPlan) : AuditDetailsResponse;
