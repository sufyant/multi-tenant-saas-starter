namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The organisation, with its name of that moment.</summary>
public sealed record FeatureFlagTenantListedDetailsResponse(Guid TenantId, string TenantName) : AuditDetailsResponse;
