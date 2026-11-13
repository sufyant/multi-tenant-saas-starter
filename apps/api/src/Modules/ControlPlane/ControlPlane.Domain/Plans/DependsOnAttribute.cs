namespace ControlPlane.Domain.Plans;

/// <summary>
/// Marks a limit of <see cref="LimitKeys"/> as one that depends on a feature of <see cref="FeatureKeys"/>: its value changes only while
/// the feature is on, and a feature that is off already refuses what the limit counts (E1, E5). The declaration names the feature once;
/// <see cref="EntitlementKeys.Dependencies"/> reads it, and <c>apps/api/scripts/generate-entitlements.mjs</c> writes it for the screens.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
internal sealed class DependsOnAttribute(string feature) : Attribute
{
    public string Feature { get; } = feature;
}
