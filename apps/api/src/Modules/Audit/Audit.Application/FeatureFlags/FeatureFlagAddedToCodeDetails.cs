using Audit.Application.AuditLogs;

namespace Audit.Application.FeatureFlags;

/// <summary>The details of <c>feature_flag.added_to_code</c>: the commit of the deploy, when it named one, and the flag's default value.</summary>
public sealed record FeatureFlagAddedToCodeDetails(string? Revision, bool DefaultValue) : AuditDetails;
