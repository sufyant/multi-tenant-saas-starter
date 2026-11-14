using ControlPlane.Domain.FeatureFlags;

namespace ControlPlane.Application.FeatureFlags;

/// <summary>One flag's page (12.05, 12.10 to 12.12, 12.18): what the code says of it, its three rules, its cleanup and its history.</summary>
public sealed record FeatureFlagDetailView(
    FeatureFlagListItemView Flag,
    string Description,
    bool DefaultValue,
    DateTimeOffset AddedAt,
    string? AddedRevision,
    string? RemovedRevision,
    IReadOnlyList<FeatureFlagTenantView> Listed,
    IReadOnlyList<FeatureFlagChangeView> History);
