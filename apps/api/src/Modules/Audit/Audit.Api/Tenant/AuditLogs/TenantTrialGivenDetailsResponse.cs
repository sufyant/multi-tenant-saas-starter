namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantTrialGivenDetailsResponse(AuditPlanResponse Plan, DateTimeOffset EndsAt) : AuditDetailsResponse;
