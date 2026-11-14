namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The commit of the deploy that archived the flag, or null when it named none.</summary>
public sealed record FeatureFlagRemovedFromCodeDetailsResponse(string? Revision) : AuditDetailsResponse;
