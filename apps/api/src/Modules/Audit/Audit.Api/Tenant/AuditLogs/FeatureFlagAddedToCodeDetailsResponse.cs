namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The commit of the deploy that stored the flag, or null when it named none, and the flag's default value.</summary>
public sealed record FeatureFlagAddedToCodeDetailsResponse(string? Revision, bool DefaultValue) : AuditDetailsResponse;
