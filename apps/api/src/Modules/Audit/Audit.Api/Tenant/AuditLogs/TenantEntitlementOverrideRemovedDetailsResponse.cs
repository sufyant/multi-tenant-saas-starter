namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantEntitlementOverrideRemovedDetailsResponse(string Key, AuditEntitlementValueResponse OldValue, DateTimeOffset? OldExpiresAt) : AuditDetailsResponse;
