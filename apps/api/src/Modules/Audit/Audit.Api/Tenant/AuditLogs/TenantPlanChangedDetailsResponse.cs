namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantPlanChangedDetailsResponse(AuditPlanResponse OldPlan, AuditPlanResponse NewPlan) : AuditDetailsResponse;
