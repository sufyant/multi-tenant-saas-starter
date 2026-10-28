namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantCreatedDetailsResponse(AuditPlanResponse Plan) : AuditDetailsResponse;
