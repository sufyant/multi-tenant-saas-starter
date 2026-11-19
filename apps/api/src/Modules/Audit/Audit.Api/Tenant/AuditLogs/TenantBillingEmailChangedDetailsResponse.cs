namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantBillingEmailChangedDetailsResponse(bool Removed) : AuditDetailsResponse;
