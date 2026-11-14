namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>Whether the flag is on for everyone else, its third rule.</summary>
public sealed record SetFeatureFlagEveryoneElseRequest(bool On);
