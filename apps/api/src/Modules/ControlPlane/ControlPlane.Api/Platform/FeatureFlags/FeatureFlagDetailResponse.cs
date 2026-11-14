namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>
/// One flag's page: the flag as the list shows it, what the code says of it (its description and its default value, which applies when the
/// rules cannot be read), the day and the commit that added it and that removed it from the code, the organisations rule 1 lists, and its
/// history, newest first.
/// </summary>
public sealed record FeatureFlagDetailResponse(
    FeatureFlagResponse Flag,
    string Description,
    bool DefaultValue,
    DateTimeOffset AddedAt,
    string? AddedRevision,
    string? RemovedRevision,
    IReadOnlyList<FeatureFlagTenantResponse> Listed,
    IReadOnlyList<FeatureFlagChangeResponse> History);
