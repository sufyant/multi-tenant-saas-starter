namespace ControlPlane.Infrastructure.Persistence.FeatureFlags;

/// <summary>One flag's rules for one organisation: whether rule 1 lists it, the percentage of rule 2 and rule 3.</summary>
internal sealed record FeatureFlagRuleRow(string Key, bool Listed, int Percentage, bool EveryoneElse);
