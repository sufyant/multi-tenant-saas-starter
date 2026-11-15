namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantPlanChangeScheduledDetailsResponse(AuditPlanResponse OldPlan, AuditPlanResponse NewPlan, DateTimeOffset AppliesAt) : AuditDetailsResponse;
