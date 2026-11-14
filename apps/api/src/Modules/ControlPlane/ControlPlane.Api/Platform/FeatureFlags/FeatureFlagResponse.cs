namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>
/// A flag as the list shows it: its key and type, its three rules in short (how many organisations rule 1 lists, the percentage of rule 2,
/// and rule 3), its cleanup with the moment it counts from, the moment the code removed it, and its latest change. A flag the code
/// removed has <c>removedAt</c> and the cleanup <c>in_use</c>.
/// </summary>
public sealed record FeatureFlagResponse(
    string Key,
    FeatureFlagResponseType Type,
    int ListedCount,
    int Percentage,
    bool EveryoneElse,
    FeatureFlagResponseCleanup Cleanup,
    DateTimeOffset? CleanupSince,
    DateTimeOffset? RemovedAt,
    FeatureFlagLastChangeResponse LastChange);
