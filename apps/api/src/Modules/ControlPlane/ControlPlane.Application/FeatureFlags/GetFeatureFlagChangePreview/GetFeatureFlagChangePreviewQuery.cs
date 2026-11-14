using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.GetFeatureFlagChangePreview;

/// <summary>
/// How many organisations one change of a flag's rules would turn on or off, before the change (12.06, 12.08, 12.09, 12.11). It names
/// exactly one change: an organisation to list, one to remove, a percentage, or everyone else on or off.
/// </summary>
[MessageName("get-feature-flag-change-preview")]
public sealed record GetFeatureFlagChangePreviewQuery(string Key, Guid? TenantToAdd, Guid? TenantToRemove, int? Percentage, bool? EveryoneElse);
