namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>
/// Every flag of this environment, by key, and the counts of the list's tabs: All counts the flags the code defines; a flag the code removed
/// counts only in its own tab (12.01 to 12.04).
/// </summary>
public sealed record FeatureFlagsResponse(IReadOnlyList<FeatureFlagResponse> Flags, FeatureFlagCountsResponse Counts);
