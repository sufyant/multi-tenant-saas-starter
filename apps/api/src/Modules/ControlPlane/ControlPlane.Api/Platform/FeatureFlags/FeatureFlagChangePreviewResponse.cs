namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>How many organisations the change would turn on or off, of those that are neither purging nor purged. It never blocks the change.</summary>
public sealed record FeatureFlagChangePreviewResponse(int AffectedCount);
