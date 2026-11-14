namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>
/// Exactly one change of the flag's rules: an organisation to list, a listed organisation to remove, a whole percentage, or everyone
/// else on or off. Any other number of changes answers 400 <c>feature_flag.change_invalid</c>.
/// </summary>
public sealed record GetFeatureFlagChangePreviewRequest(Guid? TenantToAdd, Guid? TenantToRemove, int? Percentage, bool? EveryoneElse);
