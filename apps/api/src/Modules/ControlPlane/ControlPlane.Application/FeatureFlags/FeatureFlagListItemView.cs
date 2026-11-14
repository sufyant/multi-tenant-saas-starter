using ControlPlane.Application.Users;

namespace ControlPlane.Application.FeatureFlags;

/// <summary>
/// A flag as the list shows it (12.01 to 12.04, 12.19): its key and type, its three rules in short, its cleanup with the moment it counts
/// from, whether the code removed it (the type and the cleanup as Domain names them), and its latest change with the system admin who made it, or none for a deploy.
/// </summary>
public sealed record FeatureFlagListItemView(
    string Key,
    string Type,
    int ListedCount,
    int Percentage,
    bool EveryoneElse,
    string? Cleanup,
    DateTimeOffset? CleanupSince,
    DateTimeOffset? RemovedAt,
    DateTimeOffset LastChangedAt,
    UserNameView? LastChangedBy);
