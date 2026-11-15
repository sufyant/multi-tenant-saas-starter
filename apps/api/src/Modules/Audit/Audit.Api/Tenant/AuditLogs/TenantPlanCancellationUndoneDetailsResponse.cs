namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantPlanCancellationUndoneDetailsResponse(AuditPlanResponse Plan) : AuditDetailsResponse;
