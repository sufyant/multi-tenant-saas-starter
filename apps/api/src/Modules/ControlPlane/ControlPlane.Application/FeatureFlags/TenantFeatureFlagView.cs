
namespace ControlPlane.Application.FeatureFlags;

/// <summary>A flag that is on for one organisation, with the first rule that turns it on, and the percentage of rule 2 (4.09).</summary>
public sealed record TenantFeatureFlagView(string Key, string Type, string Reason, int Percentage);
