namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>The counts of the list's four tabs.</summary>
public sealed record FeatureFlagCountsResponse(int All, int ReadyToRemove, int Unused, int RemovedFromCode);
