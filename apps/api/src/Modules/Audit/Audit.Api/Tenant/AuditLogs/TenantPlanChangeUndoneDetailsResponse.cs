namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantPlanChangeUndoneDetailsResponse(AuditPlanResponse Plan) : AuditDetailsResponse;
