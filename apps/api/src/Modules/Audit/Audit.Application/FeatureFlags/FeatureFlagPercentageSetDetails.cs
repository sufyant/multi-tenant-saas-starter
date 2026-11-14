using Audit.Application.AuditLogs;

namespace Audit.Application.FeatureFlags;

/// <summary>The details of <c>feature_flag.percentage_set</c>: the percentage before and after.</summary>
public sealed record FeatureFlagPercentageSetDetails(int From, int To) : AuditDetails;
