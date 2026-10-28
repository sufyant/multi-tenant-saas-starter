namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantEntitlementOverrideSetDetailsResponse(
    string Key,
    AuditEntitlementValueResponse? OldValue,
    DateTimeOffset? OldExpiresAt,
    AuditEntitlementValueResponse NewValue,
    DateTimeOffset? NewExpiresAt,
    string Reason) : AuditDetailsResponse;
