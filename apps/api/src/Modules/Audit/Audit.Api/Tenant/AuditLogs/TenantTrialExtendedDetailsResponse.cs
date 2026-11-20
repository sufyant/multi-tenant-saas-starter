namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantTrialExtendedDetailsResponse(DateTimeOffset OldEndsAt, DateTimeOffset NewEndsAt) : AuditDetailsResponse;
