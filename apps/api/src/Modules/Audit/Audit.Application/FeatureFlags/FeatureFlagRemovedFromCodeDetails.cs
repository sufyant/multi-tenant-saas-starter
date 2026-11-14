using Audit.Application.AuditLogs;

namespace Audit.Application.FeatureFlags;

/// <summary>The details of <c>feature_flag.removed_from_code</c>: the commit of the deploy, when it named one.</summary>
public sealed record FeatureFlagRemovedFromCodeDetails(string? Revision) : AuditDetails;
