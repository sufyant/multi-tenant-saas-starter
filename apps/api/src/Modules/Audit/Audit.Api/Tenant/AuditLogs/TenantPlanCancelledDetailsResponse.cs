namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantPlanCancelledDetailsResponse(AuditPlanResponse Plan, AuditPlanResponse NewPlan, DateTimeOffset MovesAt) : AuditDetailsResponse;
