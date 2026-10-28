namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantPlanChangedDetailsResponse(Guid OldPlanId, string OldPlanName, Guid NewPlanId, string NewPlanName) : AuditDetailsResponse;
